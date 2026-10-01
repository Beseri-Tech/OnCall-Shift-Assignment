using System.Net;
using System.Net.Http.Json;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Tests;

/// <summary>State -> district -> clinic -> officer, adding and inviting an officer in one step, and CSV import.</summary>
public class PersonnelTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private static async Task<Dictionary<string, Guid>> DistrictsAsync(HttpClient admin) =>
        (await Read<List<StateDto>>(await admin.GetAsync("/api/admin/states")))
            .SelectMany(s => s.Districts).ToDictionary(d => d.Name, d => d.Id);

    [Fact]
    public async Task Perlis_with_Kangar_and_Arau_is_seeded_and_clinics_sit_under_a_district()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);

        var states = await Read<List<StateDto>>(await admin.GetAsync("/api/admin/states"));
        var perlis = Assert.Single(states);
        Assert.Equal("Perlis", perlis.Name);
        Assert.Equal(["Arau", "Kangar"], perlis.Districts.Select(d => d.Name));
        var arau = perlis.Districts.Single(d => d.Name == "Arau");

        // A clinic needs a real district; a district with clinics, or a state with districts, can't be removed.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Arau", Guid.NewGuid()))).StatusCode);
        await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Arau", arau.Id)));
        var clinic = Assert.Single(await Read<List<ClinicDto>>(await admin.GetAsync("/api/admin/clinics")));
        Assert.Equal(("KP Arau", "Arau", "Perlis"), (clinic.Name, clinic.District, clinic.State));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/districts/{arau.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/states/{perlis.Id}")).StatusCode);

        // States and districts are data the admin can extend.
        var kedah = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/states", new UpsertStateRequest("Kedah")));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/districts", new UpsertDistrictRequest("arau", perlis.Id))).StatusCode);
        await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/districts", new UpsertDistrictRequest("Arau", kedah)));
        Assert.Equal(2, (await Read<List<StateDto>>(await admin.GetAsync("/api/admin/states"))).Count);
    }

    [Fact]
    public async Task Adding_an_officer_with_an_email_invites_them_in_the_same_step()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var kangar = (await DistrictsAsync(admin))["Kangar"];
        var clinicId = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Kangar", kangar)));

        // Bad email or a supervisor role: nothing is saved.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Ali", null, ClinicId: clinicId, Email: "not-an-email"), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Ali", null, ClinicId: clinicId, Email: "ali@test.local", Role: AccountRole.Supervisor),
            ApiFixture.Json)).StatusCode);
        Assert.Empty(await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people")));

        var saved = await Read<SavePersonResult>(await admin.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Ali", null, ClinicId: clinicId, Email: " Ali@Test.Local "), ApiFixture.Json));
        Assert.NotNull(saved.Invite?.TempPassword);   // no SMTP in tests

        var person = Assert.Single(await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people")));
        Assert.Equal(("ali@test.local", AccountRole.Officer, "Kangar", "Perlis", true), (person.Email, person.Role, person.District, person.State, person.InvitePending));

        var officer = app.CreateClient(new() { HandleCookies = true });
        var me = await Read<MeDto>(await officer.PostAsJsonAsync("/api/auth/login",
            new { email = "ali@test.local", password = saved.Invite!.TempPassword }));
        Assert.Equal(person.Id, me.PersonId);

        // Without an email it's just the officer; an email added later on the edit form invites them then.
        var later = await Read<SavePersonResult>(await admin.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Bala", null, ClinicId: clinicId), ApiFixture.Json));
        Assert.Null(later.Invite);
        var edited = await Read<SavePersonResult>(await admin.PutAsJsonAsync($"/api/admin/people/{later.Id}",
            new UpsertPersonRequest("Dr Bala", null, ClinicId: clinicId, Email: "bala@test.local", Role: AccountRole.Admin),
            ApiFixture.Json));
        Assert.NotNull(edited.Invite);
        var accounts = await Read<List<AccountDto>>(await admin.GetAsync("/api/admin/accounts"));
        Assert.Equal(AccountRole.Admin, accounts.Single(a => a.PersonId == later.Id).Role);
    }

    [Fact]
    public async Task Csv_import_checks_every_row_before_adding_anyone()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var districts = await DistrictsAsync(admin);
        await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Kangar", districts["Kangar"])));
        await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Arau", districts["Arau"])));
        await Read<SavePersonResult>(await admin.PostAsJsonAsync("/api/admin/people", new UpsertPersonRequest("Dr Existing", "D007"),
            ApiFixture.Json));

        const string bad = """
            Name,Code,Email,Role,Phone,State,District,Clinic
            Dr Ali,,ali@test.local,,012-1,Perlis,Kangar,KP Kangar
            Dr Existing,,,,,Perlis,Kangar,KP Kangar
            Dr Chong,D007,chong-at-test,Boss,,Perlis,Kangar,KP Arau
            Dr Devi,,ALI@test.local,,,Kedah,Kangar,KP Kangar
            """;
        var check = await Read<ImportResultDto>(await admin.PostAsJsonAsync("/api/admin/people/import",
            new ImportPeopleRequest(bad, Commit: true), ApiFixture.Json));
        Assert.False(check.Committed);
        Assert.Equal([2, 3, 4, 5], check.Rows.Select(r => r.Line));
        Assert.Empty(check.Rows[0].Errors);
        Assert.Contains("already in the list", Assert.Single(check.Rows[1].Errors));
        Assert.Equal(4, check.Rows[2].Errors.Count);   // code taken, bad email, bad role, clinic in another district
        Assert.Contains(check.Rows[2].Errors, e => e.Contains("KP Arau is in Arau"));
        Assert.Contains(check.Rows[3].Errors, e => e.Contains("Same email as line 2"));
        Assert.Contains(check.Rows[3].Errors, e => e.Contains("Unknown state Kedah"));
        Assert.Single(await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people")));

        // Semicolons (Excel in some locales), quoted fields, Malay headers and any letter case are fine.
        const string good = "Nama;Emel;Peranan;Negeri;Daerah;Klinik\r\n" +
                            "\"Dr Ali; bin Abu\";ali@test.local;admin;perlis;KANGAR;kp kangar\r\n" +
                            "Dr Chong;;;Perlis;Arau;KP Arau\r\n";
        var preview = await Read<ImportResultDto>(await admin.PostAsJsonAsync("/api/admin/people/import",
            new ImportPeopleRequest(good, Commit: false), ApiFixture.Json));
        Assert.All(preview.Rows, r => Assert.Empty(r.Errors));
        Assert.False(preview.Committed);

        var done = await Read<ImportResultDto>(await admin.PostAsJsonAsync("/api/admin/people/import",
            new ImportPeopleRequest(good, Commit: true), ApiFixture.Json));
        Assert.Equal((true, 2, 1), (done.Committed, done.Added, done.Invited));
        Assert.Equal("ali@test.local", Assert.Single(done.Passwords).Email);

        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        var ali = people.Single(p => p.Name == "Dr Ali; bin Abu");
        Assert.Equal(("KP Kangar", "Kangar", "Perlis", AccountRole.Admin, "D008"), (ali.ClinicName, ali.District, ali.State, ali.Role, ali.Code));
        Assert.Equal(("KP Arau", null), (people.Single(p => p.Name == "Dr Chong").ClinicName, people.Single(p => p.Name == "Dr Chong").Email));

        Assert.Contains("Missing column(s): clinic", Assert.Single((await Read<ImportResultDto>(await admin.PostAsJsonAsync(
            "/api/admin/people/import", new ImportPeopleRequest("Name,State,District\nDr X,Perlis,Kangar", true), ApiFixture.Json))).FileErrors));
    }

    [Fact]
    public void Csv_reader_handles_quotes_line_breaks_and_tabs()
    {
        var rows = Csv.Read("\uFEFFa,b\r\n\"x, \"\"y\"\"\",\"two\nlines\"\r\n\r\n  c , d ");
        Assert.Equal(["1|a|b", "2|x, \"y\"|two\nlines", "5|c|d"], rows.Select(r => $"{r.Line}|{string.Join("|", r.Fields)}"));
        Assert.Equal(["a", "b,c"], Csv.Read("a\tb,c")[0].Fields);
    }
}

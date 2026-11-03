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
        var existing = Assert.Single(check.Rows[1].Errors);
        Assert.Equal(("Name", "A"), (existing.Column, existing.Letter));
        Assert.Contains("already in the officer list", existing.Message);
        // Code taken, bad email, bad role, clinic in another district: each names its column.
        Assert.Equal(["Code", "Email", "Role", "District"], check.Rows[2].Errors.Select(e => e.Column));
        Assert.Contains(check.Rows[2].Errors, e => e.Message.Contains("KP Arau is in Arau"));
        Assert.Contains(check.Rows[3].Errors, e => e is { Column: "Email", Letter: "C" } && e.Message.Contains("Same email as row 2"));
        Assert.Contains(check.Rows[3].Errors, e => e is { Column: "State", Letter: "F" } && e.Message.Contains("Kedah is not set up")
            && e.Message.Contains("States set up: Perlis"));
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

    /// <summary>The shape of a real export from Excel: BOM, CRLF, stray spaces in cells, then hundreds of rows of bare commas
    /// where the sheet once had content. Only the one officer in a state that isn't set up should be reported.</summary>
    [Fact]
    public async Task Csv_import_from_excel_reports_only_the_real_problem_with_its_row_and_column()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var districts = await DistrictsAsync(admin);
        await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Kuala Perlis", districts["Arau"])));
        await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Kangar", districts["Kangar"])));

        var csv = "\uFEFFName,Code,Email,Role,Phone,State,District,Clinic\r\n" +
                  "Dr Aisyah Tan ,,aisyah@test.local,Officer,0135315972,Perlis,Arau,KP Kuala Perlis\r\n" +
                  "Dr Wong,,wong@test.local ,Officer,0174590887 ,Perlis,Kangar ,KP Kangar\r\n" +
                  "Dr. Chan,,chan@test.local,Officer,0174101529,Kedah,Kubang Pasu,KP Ayer Hitam\r\n" +
                  string.Concat(Enumerable.Repeat(",,,,,,,\r\n", 960));

        var check = await Read<ImportResultDto>(await admin.PostAsJsonAsync("/api/admin/people/import",
            new ImportPeopleRequest(csv, Commit: false), ApiFixture.Json));
        Assert.Empty(check.FileErrors);
        Assert.Equal([2, 3, 4], check.Rows.Select(r => r.Line));
        Assert.Equal(("Dr Aisyah Tan", "wong@test.local", "0174590887", "Kangar"),
            (check.Rows[0].Name, check.Rows[1].Email, check.Rows[1].Phone, check.Rows[1].District));
        var error = Assert.Single(check.Rows.SelectMany(r => r.Errors));
        Assert.Equal(("State", "F"), (error.Column, error.Letter));
        Assert.StartsWith("Kedah is not set up as a state", error.Message);
        Assert.Equal(4, check.Rows.Single(r => r.Errors.Count > 0).Line);
    }

    [Fact]
    public void Csv_column_letters_match_a_spreadsheet()
    {
        Assert.Equal(["A", "F", "Z", "AA", "AB"], new[] { 0, 5, 25, 26, 27 }.Select(Rota.Api.Endpoints.AdminImportEndpoints.ColumnLetter));
    }

    [Fact]
    public void Csv_reader_handles_quotes_line_breaks_and_tabs()
    {
        var rows = Csv.Read("\uFEFFa,b\r\n\"x, \"\"y\"\"\",\"two\nlines\"\r\n\r\n  c , d ");
        Assert.Equal(["1|a|b", "2|x, \"y\"|two\nlines", "5|c|d"], rows.Select(r => $"{r.Line}|{string.Join("|", r.Fields)}"));
        Assert.Equal(["a", "b,c"], Csv.Read("a\tb,c")[0].Fields);
    }
}

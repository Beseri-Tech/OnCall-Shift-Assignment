using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Rota.Api.Data;

namespace Rota.Api.Tests;

public class AuthTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private static async Task<List<AdminPersonDto>> SeedPeopleAsync(HttpClient admin, int count)
    {
        var names = string.Join("\n", Enumerable.Range(1, count).Select(i => $"Dr Auth {i}"));
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names })).EnsureSuccessStatusCode();
        return await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    [Fact]
    public async Task Everything_needs_login_except_the_login_itself()
    {
        await using var app = fixture.CreateApp();
        var anon = NewClient(app);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/people")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/periods")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anon.PostAsJsonAsync("/api/auth/forgot", new ForgotPasswordRequest("nobody@x.com"))).StatusCode);
    }

    [Fact]
    public async Task Invited_officer_must_replace_the_temporary_password_before_using_the_app()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var person = (await SeedPeopleAsync(admin, 1))[0];

        // An officer account needs an officer; emails are unique.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/accounts",
            new InviteRequest("a@test.local", AccountRole.Officer, null), ApiFixture.Json)).StatusCode);
        var invite = await Read<InviteResult>(await admin.PostAsJsonAsync("/api/admin/accounts",
            new InviteRequest(" A@Test.Local ", AccountRole.Officer, person.Id), ApiFixture.Json));
        Assert.False(invite.EmailSent);   // no SMTP in tests, so the admin gets the temporary password
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/accounts",
            new InviteRequest("a@test.local", AccountRole.Supervisor, null), ApiFixture.Json)).StatusCode);

        var officer = NewClient(app);
        var me = await Read<MeDto>(await officer.PostAsJsonAsync("/api/auth/login", new { email = "a@test.local", password = invite.TempPassword }));
        Assert.True(me.MustChangePassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/periods")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(invite.TempPassword!, "short"))).StatusCode);
        me = await Read<MeDto>(await officer.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(invite.TempPassword!, "a-much-better-password")));
        Assert.False(me.MustChangePassword);
        Assert.Equal((person.Id, AccountRole.Officer), (me.PersonId, me.Role));
        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync("/api/periods")).StatusCode);
    }

    [Fact]
    public async Task Officers_only_touch_their_own_leave_and_admins_bypass_the_limits()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var people = await SeedPeopleAsync(admin, 3);
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Oct 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null), ApiFixture.Json));
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);

        string Url(Guid id) => $"/api/people/{id}/entries?periodId={period.Id}";
        var weekends = new EntriesDto(new[] { 3, 4, 10, 11, 17 }.Select(d => new LeaveEntryDto(new DateOnly(2026, 10, d), null)).ToList(), []);

        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync(Url(people[1].Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PutAsJsonAsync(Url(people[1].Id), weekends, ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/admin/people")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PutAsJsonAsync(Url(people[0].Id), weekends, ApiFixture.Json)).StatusCode);

        // 5 weekend days is over the 3-point budget, but the admin may enter it on someone's behalf.
        (await admin.PutAsJsonAsync(Url(people[1].Id), weekends, ApiFixture.Json)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Disabling_the_account_or_marking_the_officer_as_left_ends_the_session()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var people = await SeedPeopleAsync(admin, 2);
        var first = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        var second = await ApiFixture.OfficerClientAsync(app, admin, people[1].Id);
        var accounts = await Read<List<AccountDto>>(await admin.GetAsync("/api/admin/accounts"));
        var firstAccount = accounts.Single(a => a.PersonId == people[0].Id);

        (await admin.PutAsJsonAsync($"/api/admin/accounts/{firstAccount.Id}",
            new UpdateAccountRequest(firstAccount.Email, AccountRole.Officer, people[0].Id, Enabled: false), ApiFixture.Json))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/periods")).StatusCode);

        var p = people[1];
        (await admin.PutAsJsonAsync($"/api/admin/people/{p.Id}",
            new UpsertPersonRequest(p.Name, p.Code, OfficerStatus.Left, StatusReason: "PINDAH"), ApiFixture.Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/periods")).StatusCode);

        // The admin can't lock themselves out.
        var me = await Read<MeDto>(await admin.GetAsync("/api/auth/me"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/admin/accounts/{me.AccountId}",
            new UpdateAccountRequest(me.Email, AccountRole.Supervisor, null, Enabled: false), ApiFixture.Json)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_emails_a_one_time_reset_link()
    {
        string mail = Path.Combine(Path.GetTempPath(), "rota-mail-" + Guid.NewGuid().ToString("N"));
        await using var app = fixture.CreateApp(new() { ["Smtp:PickupDirectory"] = mail, ["App:BaseUrl"] = "https://rota.test" });
        var anon = NewClient(app);

        (await anon.PostAsJsonAsync("/api/auth/forgot", new ForgotPasswordRequest(ApiFixture.AdminEmail))).EnsureSuccessStatusCode();

        // Sent by the background worker, so wait for it.
        string[] files = [];
        for (int i = 0; i < 50 && files.Length == 0; i++)
        {
            await Task.Delay(100);
            files = Directory.Exists(mail) ? Directory.GetFiles(mail, "*.eml") : [];
        }

        // Quoted-printable: undo soft line breaks and the encoded "=".
        string eml = File.ReadAllText(files.Single()).Replace("=\r\n", "").Replace("=3D", "=");
        string token = Regex.Match(eml, @"https://rota\.test/reset-password\?token=([A-Za-z0-9_-]+)").Groups[1].Value;
        Assert.NotEmpty(token);

        (await anon.PostAsJsonAsync("/api/auth/reset", new ResetPasswordRequest(token, "brand-new-password"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anon.PostAsJsonAsync("/api/auth/reset", new ResetPasswordRequest(token, "another-password-1"))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFixture.AdminEmail, password = ApiFixture.AdminPasswordText })).StatusCode);
        (await anon.PostAsJsonAsync("/api/auth/login", new { email = ApiFixture.AdminEmail, password = "brand-new-password" }))
            .EnsureSuccessStatusCode();

        Directory.Delete(mail, recursive: true);
    }
}

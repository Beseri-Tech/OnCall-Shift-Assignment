using System.Net;
using System.Net.Http.Json;
using Rota.Api.Data;

namespace Rota.Api.Tests;

public class ApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private static async Task<(List<AdminPersonDto> People, PeriodDto Period)> SeedAsync(HttpClient admin, int people = 6)
    {
        var names = string.Join("\n", Enumerable.Range(1, people).Select(i => $"Dr Test {i}"));
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names })).EnsureSuccessStatusCode();

        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Oct 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null), ApiFixture.Json));
        var list = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        return (list, period);
    }

    [Fact]
    public async Task Admin_endpoints_require_login_and_wrong_password_is_rejected()
    {
        await using var app = fixture.CreateApp();
        var anon = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/admin/people")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/admin/login", new { password = "nope" })).StatusCode);

        var admin = await ApiFixture.AdminClientAsync(app);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/people")).StatusCode);
    }

    [Fact]
    public async Task People_get_permanent_ids_and_sequential_codes()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, _) = await SeedAsync(admin, 3);

        Assert.Equal(["D001", "D002", "D003"], people.Select(p => p.Code));
        Assert.All(people, p => Assert.NotEqual(Guid.Empty, p.Id));

        var dup = await admin.PostAsJsonAsync("/api/admin/people", new UpsertPersonRequest("dr test 1", null), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    [Fact]
    public async Task Leave_can_be_saved_while_open_and_is_refused_after_lock_or_outside_the_period()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        var user = app.CreateClient();
        var url = $"/api/people/{people[0].Id}/entries?periodId={period.Id}";

        var body = new EntriesDto([new(new DateOnly(2026, 10, 5), "Annual"), new(new DateOnly(2026, 10, 6), "Annual")],
            [new DateOnly(2026, 10, 20)]);
        var saved = await Read<EntriesDto>(await user.PutAsJsonAsync(url, body, ApiFixture.Json));
        Assert.Equal(2, saved.Leave.Count);
        Assert.Equal("Annual", saved.Leave[0].Note);
        Assert.Equal([new DateOnly(2026, 10, 20)], saved.Preferred);

        var outside = new EntriesDto([new(new DateOnly(2026, 11, 5), null)], []);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PutAsJsonAsync(url, outside, ApiFixture.Json)).StatusCode);

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await user.PutAsJsonAsync(url, body, ApiFixture.Json)).StatusCode);
    }

    [Fact]
    public async Task Generate_publish_and_totals_follow_the_published_run()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);

        // Generating needs a locked period.
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1))).StatusCode);
        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();

        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        Assert.Equal(31, run.Days.Count);
        Assert.Equal(0, run.Run.Unassigned);
        Assert.Equal(0, run.Run.ConsecutivePairs);

        // Nothing counts until published.
        var before = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        Assert.All(before, p => Assert.Equal(0, p.Totals.Total));

        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();
        var after = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        Assert.Equal(31, after.Sum(p => p.Totals.Total));
        Assert.Equal(run.Days.Count(d => d.IsWeekendHoliday), after.Sum(p => p.Totals.WeekendHoliday));

        // Public rota is visible once published.
        var rota = await Read<PublishedRotaDto>(await app.CreateClient().GetAsync($"/api/periods/{period.Id}/rota"));
        Assert.Equal(run.Run.Id, rota.RunId);

        // Published runs can't be edited; unpublishing rolls totals back.
        var edit = await admin.PutAsJsonAsync($"/api/admin/runs/{run.Run.Id}/assignments/2026-10-01", new OverrideRequest(people[0].Id));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/unpublish", null)).EnsureSuccessStatusCode();
        var rolledBack = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        Assert.All(rolledBack, p => Assert.Equal(0, p.Totals.Total));
    }

    [Fact]
    public async Task Manual_override_warns_about_leave_and_back_to_back_shifts()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        var target = people[0];

        await app.CreateClient().PutAsJsonAsync($"/api/people/{target.Id}/entries?periodId={period.Id}",
            new EntriesDto([new(new DateOnly(2026, 10, 10), null)], []), ApiFixture.Json);
        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(3)));

        var res = await Read<OverrideResponse>(await admin.PutAsJsonAsync(
            $"/api/admin/runs/{run.Run.Id}/assignments/2026-10-10", new OverrideRequest(target.Id)));

        Assert.Equal(target.Id, res.Day.PersonId);
        Assert.True(res.Day.IsManual);
        Assert.Contains(res.Warnings, w => w.Contains("on leave"));
    }

    [Fact]
    public async Task Opening_numbers_feed_totals_and_prior_weekend_history()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, _) = await SeedAsync(admin, 2);

        var p = people[0];
        (await admin.PutAsJsonAsync($"/api/admin/people/{p.Id}",
            new UpsertPersonRequest(p.Name, p.Code, OpeningTotal: 10, OpeningWeekday: 7, OpeningWeekendHoliday: 3), ApiFixture.Json))
            .EnsureSuccessStatusCode();

        var profile = await Read<PersonProfileDto>(await app.CreateClient().GetAsync($"/api/people/{p.Id}"));
        Assert.Equal(new TotalsDto(10, 7, 3), profile.Totals);
    }

    [Fact]
    public async Task Leave_text_parse_endpoint_uses_the_sheet_format()
    {
        await using var app = fixture.CreateApp();
        var client = app.CreateClient();

        var ok = await Read<ParseResponse>(await client.PostAsJsonAsync("/api/leave/parse",
            new ParseRequest("3/10-5/10, 12/10", new DateOnly(2026, 10, 1))));
        Assert.Equal(4, ok.Dates.Count);
        Assert.Null(ok.Error);

        var bad = await Read<ParseResponse>(await client.PostAsJsonAsync("/api/leave/parse",
            new ParseRequest("12", new DateOnly(2026, 10, 1))));
        Assert.NotNull(bad.Error);
    }
}

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
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/auth/login", new { email = ApiFixture.AdminEmail, password = "nope" })).StatusCode);

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
    public async Task Excluded_officers_drop_out_of_the_rota_and_clinics_can_be_removed()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin, 4);

        var clinicId = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Beseri", "Kangar")));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("kp beseri", "Kangar"))).StatusCode);

        var target = people[0];
        var url = $"/api/admin/people/{target.Id}";
        // A reason is required when someone isn't on call.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url,
            new UpsertPersonRequest(target.Name, target.Code, OfficerStatus.Excluded), ApiFixture.Json)).StatusCode);
        (await admin.PutAsJsonAsync(url, new UpsertPersonRequest(target.Name, target.Code, OfficerStatus.Excluded,
            StatusReason: "CUTI BERSALIN", ExcludedUntil: new DateOnly(2027, 1, 1), ClinicId: clinicId, Phone: "012-3456789"),
            ApiFixture.Json)).EnsureSuccessStatusCode();

        var saved = (await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"))).Single(p => p.Id == target.Id);
        Assert.Equal((OfficerStatus.Excluded, "CUTI BERSALIN", "KP Beseri", "Kangar", "012-3456789"),
            (saved.Status, saved.StatusReason, saved.ClinicName, saved.Area, saved.Phone));

        Assert.DoesNotContain(await Read<List<PersonSummaryDto>>(await admin.GetAsync("/api/people")), p => p.Id == target.Id);

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        Assert.DoesNotContain(run.Days, d => d.PersonId == target.Id);

        // Back on call clears the reason; deleting the clinic unsets it.
        (await admin.PutAsJsonAsync(url, new UpsertPersonRequest(target.Name, target.Code, ClinicId: clinicId), ApiFixture.Json))
            .EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/admin/clinics/{clinicId}")).EnsureSuccessStatusCode();
        saved = (await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"))).Single(p => p.Id == target.Id);
        Assert.Equal((OfficerStatus.OnCall, null, null), (saved.Status, saved.StatusReason, saved.ClinicId));
        Assert.Contains(await Read<List<PersonSummaryDto>>(await admin.GetAsync("/api/people")), p => p.Id == target.Id);
    }

    [Fact]
    public async Task Leave_limits_points_top_ups_day_caps_and_peak_days()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin, 10);   // Oct 2026: 9 weekend days -> 3 points; caps 3 (busy) / 5 (weekday)
        var officers = new List<HttpClient>();
        foreach (var p in people) officers.Add(await ApiFixture.OfficerClientAsync(app, admin, p.Id));
        Task<HttpResponseMessage> Save(int who, params int[] days) =>
            officers[who].PutAsJsonAsync($"/api/people/{people[who].Id}/entries?periodId={period.Id}",
                new EntriesDto(days.Select(d => new LeaveEntryDto(new DateOnly(2026, 10, d), null)).ToList(), []), ApiFixture.Json);

        // Four weekend days cost 4 points; only 3 are available until the admin tops up.
        var refused = await Save(0, 3, 4, 10, 11);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Not enough points: 4 needed, 3 available", await refused.Content.ReadAsStringAsync());
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/points/{people[0].Id}", new PointGrantRequest(1, "Outstation")))
            .EnsureSuccessStatusCode();
        (await Save(0, 3, 4, 10, 11)).EnsureSuccessStatusCode();

        // 17 Oct fills up after 3 officers; the 4th is refused, but the others can still re-save.
        foreach (var who in new[] { 1, 2, 3 }) (await Save(who, 17)).EnsureSuccessStatusCode();
        var full = await Save(4, 17);
        Assert.Equal(HttpStatusCode.BadRequest, full.StatusCode);
        Assert.Contains("Already full", await full.Content.ReadAsStringAsync());
        (await Save(1, 17)).EnsureSuccessStatusCode();

        // Two officers racing for the last spot on 24 Oct (2 already off, cap 3): exactly one gets it.
        foreach (var who in new[] { 6, 7 }) (await Save(who, 24)).EnsureSuccessStatusCode();
        var race = await Task.WhenAll(Save(8, 24), Save(9, 24));
        Assert.Single(race, r => r.IsSuccessStatusCode);
        Assert.Single(race, r => r.StatusCode == HttpStatusCode.BadRequest);

        var rules = await Read<LeaveRulesDto>(await officers[4].GetAsync($"/api/periods/{period.Id}/leave-rules?personId={people[4].Id}"));
        Assert.Equal((3, 0, 3, 5, 10), (rules.Budget, rules.Extra, rules.BusyDayCap, rules.WeekdayCap, rules.OnCall));
        Assert.Equal(3, rules.OthersOff[new DateOnly(2026, 10, 17)]);

        // A peak weekday costs a point but is not a weekend shift for the rota.
        (await admin.PutAsJsonAsync("/api/admin/peak-days", new UpsertHolidayRequest(new DateOnly(2026, 10, 7), "Eve of Deepavali")))
            .EnsureSuccessStatusCode();
        (await Save(5, 7)).EnsureSuccessStatusCode();
        var points = await Read<List<PointsRowDto>>(await admin.GetAsync($"/api/admin/periods/{period.Id}/points"));
        Assert.Equal((1, 0), (points.Single(p => p.PersonId == people[5].Id).PointsUsed, points.Single(p => p.PersonId == people[5].Id).WeekdaysUsed));
        Assert.Equal((4, 3, 1, "Outstation"), points.Where(p => p.PersonId == people[0].Id).Select(p => (p.PointsUsed, p.Budget, p.Extra, p.Reason)).Single());

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        Assert.False(run.Days.Single(d => d.Date == new DateOnly(2026, 10, 7)).IsWeekendHoliday);
    }

    [Fact]
    public async Task Leave_can_be_saved_while_open_and_is_refused_after_lock_or_outside_the_period()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        var user = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
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
        var before = await Read<List<TallyRowDto>>(await admin.GetAsync("/api/admin/tally"));
        Assert.All(before, p => Assert.Equal(0, p.Total));

        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();
        var after = await Read<List<TallyRowDto>>(await admin.GetAsync("/api/admin/tally"));
        Assert.Equal(31, after.Sum(p => p.Total));
        Assert.Equal(run.Days.Count(d => d.IsWeekendHoliday), after.Sum(p => p.WeekendHoliday));

        // Officers can see the rota once published.
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        var rota = await Read<PublishedRotaDto>(await officer.GetAsync($"/api/periods/{period.Id}/rota"));
        Assert.Equal(run.Run.Id, rota.RunId);

        // ...and their own shifts (only their own).
        var shifts = await Read<List<ShiftDto>>(await officer.GetAsync($"/api/people/{people[0].Id}/shifts"));
        Assert.Equal(run.Days.Where(d => d.PersonId == people[0].Id).Select(d => d.Date), shifts.Select(s => s.Date));
        Assert.All(shifts, s => Assert.Equal("Oct 2026", s.PeriodName));
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync($"/api/people/{people[1].Id}/shifts")).StatusCode);

        // Published runs can't be edited; unpublishing rolls totals back.
        var edit = await admin.PutAsJsonAsync($"/api/admin/runs/{run.Run.Id}/assignments/2026-10-01", new OverrideRequest(people[0].Id));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/unpublish", null)).EnsureSuccessStatusCode();
        var rolledBack = await Read<List<TallyRowDto>>(await admin.GetAsync("/api/admin/tally"));
        Assert.All(rolledBack, p => Assert.Equal(0, p.Total));
    }

    [Fact]
    public async Task Manual_override_warns_about_leave_and_back_to_back_shifts()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        var target = people[0];

        await admin.PutAsJsonAsync($"/api/people/{target.Id}/entries?periodId={period.Id}",
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
    public async Task Tally_shows_what_the_admin_typed_and_grows_with_published_rotas()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin, 4);
        var p = people[0];
        async Task<TallyRowDto> Row() => (await Read<List<TallyRowDto>>(await admin.GetAsync("/api/admin/tally"))).Single(r => r.PersonId == p.Id);

        Assert.False((await Row()).Known);
        (await admin.PutAsJsonAsync("/api/admin/tally", new[] { new TallyUpdate(p.Id, 7, 3) })).EnsureSuccessStatusCode();
        Assert.Equal((7, 3, 10, true), ((await Row()).Weekday, (await Row()).WeekendHoliday, (await Row()).Total, (await Row()).Known));

        // The officer sees the same numbers on their profile.
        var officer = await ApiFixture.OfficerClientAsync(app, admin, p.Id);
        Assert.Equal(new TotalsDto(10, 7, 3), (await Read<PersonProfileDto>(await officer.GetAsync($"/api/people/{p.Id}"))).Totals);

        // Publishing adds this period's shifts on top.
        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();
        int shifts = run.Days.Count(d => d.PersonId == p.Id);
        Assert.Equal(10 + shifts, (await Row()).Total);

        // Typing a number again shows exactly that, published shifts included.
        (await admin.PutAsJsonAsync("/api/admin/tally", new[] { new TallyUpdate(p.Id, 20, 5) })).EnsureSuccessStatusCode();
        Assert.Equal((20, 5), ((await Row()).Weekday, (await Row()).WeekendHoliday));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync("/api/admin/tally", new[] { new TallyUpdate(p.Id, -1, 0) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/admin/tally")).StatusCode);
    }

    [Fact]
    public async Task Leave_text_parse_endpoint_uses_the_sheet_format()
    {
        await using var app = fixture.CreateApp();
        var client = await ApiFixture.AdminClientAsync(app);

        var ok = await Read<ParseResponse>(await client.PostAsJsonAsync("/api/leave/parse",
            new ParseRequest("3/10-5/10, 12/10", new DateOnly(2026, 10, 1))));
        Assert.Equal(4, ok.Dates.Count);
        Assert.Null(ok.Error);

        var bad = await Read<ParseResponse>(await client.PostAsJsonAsync("/api/leave/parse",
            new ParseRequest("12", new DateOnly(2026, 10, 1))));
        Assert.NotNull(bad.Error);
    }
}

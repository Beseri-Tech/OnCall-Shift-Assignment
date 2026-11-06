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

        var kangar = (await Read<List<StateDto>>(await admin.GetAsync("/api/admin/states"))).Single().Districts.Single(d => d.Name == "Kangar");
        var clinicId = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Beseri", kangar.Id)));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("kp beseri", kangar.Id))).StatusCode);

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
            (saved.Status, saved.StatusReason, saved.ClinicName, saved.District, saved.Phone));

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
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/days",
            new PeriodDayDto(new DateOnly(2026, 10, 7), PeriodDayKind.Peak, "Eve of Deepavali"), ApiFixture.Json)).EnsureSuccessStatusCode();
        (await Save(5, 7)).EnsureSuccessStatusCode();
        var points = await Read<List<PointsRowDto>>(await admin.GetAsync($"/api/admin/periods/{period.Id}/points"));
        Assert.Equal((1, 0), (points.Single(p => p.PersonId == people[5].Id).PointsUsed, points.Single(p => p.PersonId == people[5].Id).WeekdaysUsed));
        Assert.Equal((4, 3, 1, "Outstation"), points.Where(p => p.PersonId == people[0].Id).Select(p => (p.PointsUsed, p.Budget, p.Extra, p.Reason)).Single());

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        Assert.False(run.Days.Single(d => d.Date == new DateOnly(2026, 10, 7)).IsWeekendHoliday);
    }

    [Fact]
    public async Task Each_period_has_its_own_holidays_and_peak_days_started_from_the_master_lists()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        DateOnly D(int month, int day) => new(2026, month, day);
        Task<List<PeriodDayDto>> Days(Guid id) => admin.GetAsync($"/api/admin/periods/{id}/days").ContinueWith(t => Read<List<PeriodDayDto>>(t.Result)).Unwrap();
        Task<PeriodDto> NewPeriod(string name, DateOnly start, DateOnly end) =>
            admin.PostAsJsonAsync("/api/admin/periods", new UpsertPeriodRequest(name, start, end, null), ApiFixture.Json)
                .ContinueWith(t => Read<PeriodDto>(t.Result)).Unwrap();

        (await admin.PutAsJsonAsync("/api/admin/holidays", new UpsertHolidayRequest(D(10, 20), "Deepavali"))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/holidays", new UpsertHolidayRequest(D(11, 3), "State holiday"))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/peak-days", new UpsertHolidayRequest(D(10, 19), "Eve of Deepavali"))).EnsureSuccessStatusCode();

        // A new period starts with the master days inside its dates.
        var oct = await NewPeriod("Oct", D(10, 1), D(10, 31));
        Assert.Equal([new(D(10, 19), PeriodDayKind.Peak, "Eve of Deepavali"), new(D(10, 20), PeriodDayKind.Holiday, "Deepavali")],
            await Days(oct.Id));

        // Changing one period leaves the master list and other periods alone.
        (await admin.DeleteAsync($"/api/admin/periods/{oct.Id}/days/Holiday/2026-10-20")).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/periods/{oct.Id}/days", new PeriodDayDto(D(10, 24), PeriodDayKind.Holiday, "Sultan's birthday"),
            ApiFixture.Json)).EnsureSuccessStatusCode();
        var outside = await admin.PutAsJsonAsync($"/api/admin/periods/{oct.Id}/days", new PeriodDayDto(D(11, 1), PeriodDayKind.Holiday, "x"), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal([D(10, 19), D(10, 24)], (await Days(oct.Id)).Select(d => d.Date));
        Assert.Equal([D(10, 24)], (await Read<List<HolidayDto>>(await admin.GetAsync("/api/holidays?from=2026-10-01&to=2026-10-31"))).Select(h => h.Date));

        var nov = await NewPeriod("Nov", D(11, 1), D(11, 30));
        Assert.Equal([D(11, 3)], (await Days(nov.Id)).Select(d => d.Date));

        // The rota uses the period's own holidays.
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr A\nDr B\nDr C" })).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/periods/{oct.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{oct.Id}/generate", new GenerateRequest(1)));
        Assert.False(run.Days.Single(d => d.Date == D(10, 20)).IsWeekendHoliday);
        Assert.Equal("Sultan's birthday", run.Days.Single(d => d.Date == D(10, 24)).HolidayName);
        (await admin.DeleteAsync($"/api/admin/runs/{run.Run.Id}")).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/periods/{oct.Id}/unlock", null)).EnsureSuccessStatusCode();

        // Copying from the master list puts back what's missing; the period's additions stay.
        Assert.Equal(1, (await Read<Dictionary<string, int>>(await admin.PostAsync($"/api/admin/periods/{oct.Id}/days/copy-master", null)))["added"]);
        Assert.Equal([D(10, 19), D(10, 20), D(10, 24)], (await Days(oct.Id)).Select(d => d.Date));

        // Moving the dates drops days outside and adds master days for the newly covered dates.
        (await admin.PutAsJsonAsync($"/api/admin/periods/{nov.Id}", new UpsertPeriodRequest("Nov", D(11, 5), D(11, 30), null), ApiFixture.Json))
            .EnsureSuccessStatusCode();
        Assert.Empty(await Days(nov.Id));
        (await admin.PutAsJsonAsync($"/api/admin/periods/{oct.Id}", new UpsertPeriodRequest("Oct", D(10, 1), D(11, 4), null), ApiFixture.Json))
            .EnsureSuccessStatusCode();
        Assert.Equal([D(10, 19), D(10, 20), D(10, 24), D(11, 3)], (await Days(oct.Id)).Select(d => d.Date));
    }

    [Fact]
    public async Task Officer_who_was_put_over_budget_can_remove_days_but_not_swap_them()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}", new UpsertPeriodRequest(period.Name, period.StartDate, period.EndDate, null, 1),
            ApiFixture.Json)).EnsureSuccessStatusCode();
        var user = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        var url = $"/api/people/{people[0].Id}/entries?periodId={period.Id}";
        EntriesDto Days(params int[] days) => new([.. days.Select(d => new LeaveEntryDto(new DateOnly(2026, 10, d), null))], []);

        // The admin saves 3 weekend days on a budget of 1 point (admins skip the limits).
        (await admin.PutAsJsonAsync(url, Days(3, 4, 10), ApiFixture.Json)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await user.PutAsJsonAsync(url, Days(3, 4, 10), ApiFixture.Json)).StatusCode);     // unchanged
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PutAsJsonAsync(url, Days(3, 4, 11), ApiFixture.Json)).StatusCode); // swap
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PutAsJsonAsync(url, Days(3, 4, 10, 11), ApiFixture.Json)).StatusCode); // add
        Assert.Equal(HttpStatusCode.OK, (await user.PutAsJsonAsync(url, Days(3, 4), ApiFixture.Json)).StatusCode);        // remove
        Assert.Equal(HttpStatusCode.OK, (await user.PutAsJsonAsync(url, Days(3), ApiFixture.Json)).StatusCode);           // within budget
    }

    [Fact]
    public async Task Blocked_weekdays_cannot_be_marked_and_the_officer_is_never_put_on_call_then()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        var officer = people[0];
        (await admin.PutAsJsonAsync($"/api/admin/people/{officer.Id}",
            new UpsertPersonRequest(officer.Name, officer.Code, BlockedWeekdays: [(int)DayOfWeek.Sunday], BlockedReason: "Works in Kedah"),
            ApiFixture.Json)).EnsureSuccessStatusCode();
        var listed = (await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"))).Single(p => p.Id == officer.Id);
        Assert.Equal([0], listed.BlockedWeekdays.ToArray());
        Assert.Equal("Works in Kedah", listed.BlockedReason);

        var user = await ApiFixture.OfficerClientAsync(app, admin, officer.Id);
        var url = $"/api/people/{officer.Id}/entries?periodId={period.Id}";
        var sunday = new EntriesDto([new(new DateOnly(2026, 10, 4), null)], []);
        var res = await user.PutAsJsonAsync(url, sunday, ApiFixture.Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Sundays", await res.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await user.PutAsJsonAsync(url, new EntriesDto([], [new DateOnly(2026, 10, 11)]), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await user.PutAsJsonAsync(url, new EntriesDto([new(new DateOnly(2026, 10, 3), null)], []), ApiFixture.Json)).StatusCode);

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        Assert.DoesNotContain(run.Days, d => d.PersonId == officer.Id && d.Date.DayOfWeek == DayOfWeek.Sunday);
        Assert.Contains(run.Days, d => d.PersonId == officer.Id);

        // The admin can still put them on a blocked day by hand, with a warning.
        var sundaySlot = run.Days.First(d => d.Date.DayOfWeek == DayOfWeek.Sunday);
        var forced = await Read<OverrideResponse>(await admin.PutAsJsonAsync($"/api/admin/runs/{run.Run.Id}/assignments/{sundaySlot.Date:yyyy-MM-dd}",
            new OverrideRequest(officer.Id), ApiFixture.Json));
        Assert.Contains(forced.Warnings, w => w.Contains("can't be on call on Sundays"));
    }

    [Fact]
    public async Task Clinic_event_days_cannot_be_marked_and_nobody_from_the_clinic_is_put_on_call_then()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        var kangar = (await Read<List<StateDto>>(await admin.GetAsync("/api/admin/states"))).Single().Districts.Single(d => d.Name == "Kangar");
        var clinicId = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Beseri", kangar.Id)));
        var members = people.Take(3).ToList();
        foreach (var m in members)
            (await admin.PutAsJsonAsync($"/api/admin/people/{m.Id}", new UpsertPersonRequest(m.Name, m.Code, ClinicId: clinicId), ApiFixture.Json))
                .EnsureSuccessStatusCode();

        DateOnly event1 = new(2026, 10, 10), event2 = new(2026, 10, 17);
        (await admin.PostAsJsonAsync($"/api/admin/clinics/{clinicId}/blocked-days",
            new AddClinicBlockedDaysRequest([event1, event2], "Clinic annual dinner"), ApiFixture.Json)).EnsureSuccessStatusCode();
        var listed = await Read<List<ClinicBlockedDayDto>>(await admin.GetAsync("/api/admin/clinics/blocked-days?from=2026-01-01"));
        Assert.Equal([event1, event2], listed.Select(d => d.Date).ToArray());

        // Officers of the clinic can't mark leave or a preferred date on those days.
        var user = await ApiFixture.OfficerClientAsync(app, admin, members[0].Id);
        var url = $"/api/people/{members[0].Id}/entries?periodId={period.Id}";
        var res = await user.PutAsJsonAsync(url, new EntriesDto([new(event1, null)], []), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await user.PutAsJsonAsync(url, new EntriesDto([], [event2]), ApiFixture.Json)).StatusCode);
        var profile = await Read<PersonProfileDto>(await user.GetAsync($"/api/people/{members[0].Id}"));
        Assert.Equal([event1, event2], profile.ClinicBlockedDays!.Select(d => d.Date).ToArray());

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        var memberIds = members.Select(m => m.Id).ToHashSet();
        Assert.DoesNotContain(run.Days, d => (d.Date == event1 || d.Date == event2) && d.PersonId is { } id && memberIds.Contains(id));
        Assert.Contains(run.Days, d => d.Date == event1 && d.PersonId != null);

        // The admin can still force it, with a warning.
        var forced = await Read<OverrideResponse>(await admin.PutAsJsonAsync($"/api/admin/runs/{run.Run.Id}/assignments/{event1:yyyy-MM-dd}",
            new OverrideRequest(members[0].Id), ApiFixture.Json));
        Assert.Contains(forced.Warnings, w => w.Contains("clinic is blocked"));

        // Unblocking removes the day from the list.
        (await admin.DeleteAsync($"/api/admin/clinics/{clinicId}/blocked-days/{event1:yyyy-MM-dd}")).EnsureSuccessStatusCode();
        listed = await Read<List<ClinicBlockedDayDto>>(await admin.GetAsync("/api/admin/clinics/blocked-days?from=2026-01-01"));
        Assert.Equal([event2], listed.Select(d => d.Date).ToArray());
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
    public async Task Busy_day_shifts_in_a_published_rota_earn_reward_points_for_the_next_rota_only()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/days",
            new PeriodDayDto(new DateOnly(2026, 10, 5), PeriodDayKind.Holiday, "Test holiday"), ApiFixture.Json)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/days",
            new PeriodDayDto(new DateOnly(2026, 10, 7), PeriodDayKind.Peak, "Test peak"), ApiFixture.Json)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));

        var nov = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Nov 2026", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null), ApiFixture.Json));
        async Task<LeaveRulesDto> Rules(Guid periodId, Guid personId) =>
            await Read<LeaveRulesDto>(await admin.GetAsync($"/api/periods/{periodId}/leave-rules?personId={personId}"));

        // Nothing is earned until the rota is published.
        foreach (var p in people) Assert.Equal(0, (await Rules(nov.Id, p.Id)).Reward);

        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();
        int Expected(Guid id) => run.Days.Where(d => d.PersonId == id).Sum(d =>
            d.Date == new DateOnly(2026, 10, 5) ? 3 : d.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 2 : d.Date == new DateOnly(2026, 10, 7) ? 1 : 0);
        Assert.True(people.Sum(p => Expected(p.Id)) > 0);
        foreach (var p in people)
        {
            var rules = await Rules(nov.Id, p.Id);
            Assert.Equal(Expected(p.Id), rules.Reward);
            if (rules.Reward > 0) Assert.Equal("Oct 2026", rules.RewardFrom);
            Assert.Equal(0, (await Rules(period.Id, p.Id)).Reward);   // the rota that was worked gets nothing
        }
        var row = (await Read<List<PointsRowDto>>(await admin.GetAsync($"/api/admin/periods/{nov.Id}/points"))).First(r => r.Reward > 0);
        Assert.Equal("Oct 2026", row.RewardFrom);

        // A rota in between means last month's shifts no longer count: only the rota right before earns the reward.
        (await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Dec 2026", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), null), ApiFixture.Json)).EnsureSuccessStatusCode();
        var dec = (await Read<List<PeriodDto>>(await admin.GetAsync("/api/admin/periods"))).Single(x => x.Name == "Dec 2026");
        foreach (var p in people) Assert.Equal(0, (await Rules(dec.Id, p.Id)).Reward);

        // Unpublishing takes the reward away again.
        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/unpublish", null)).EnsureSuccessStatusCode();
        foreach (var p in people) Assert.Equal(0, (await Rules(nov.Id, p.Id)).Reward);
    }

    [Fact]
    public async Task Officers_left_out_of_a_period_get_no_shifts()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin);
        var left = people[0];

        var updated = await Read<PeriodDto>(await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}",
            new UpsertPeriodRequest(period.Name, period.StartDate, period.EndDate, period.LeaveDeadline, period.PointsBudget, [left.Id]), ApiFixture.Json));
        Assert.Equal([left.Id], updated.ExcludedPersonIds);
        var listed = (await Read<List<PeriodDto>>(await admin.GetAsync("/api/admin/periods"))).Single(x => x.Id == period.Id);
        Assert.Equal([left.Id], listed.ExcludedPersonIds);

        var overview = await Read<OverviewDto>(await admin.GetAsync($"/api/periods/{period.Id}/overview"));
        Assert.DoesNotContain(overview.People, p => p.Id == left.Id);
        Assert.Equal(people.Count - 1, overview.People.Count);
        Assert.All(overview.Days, d => Assert.Equal(people.Count - 1, d.Available));

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        Assert.DoesNotContain(run.Days, d => d.PersonId == left.Id);
        Assert.DoesNotContain(run.Stats, s => s.PersonId == left.Id);

        // Saving without a list keeps it; an empty list brings everyone back.
        var kept = await Read<PeriodDto>(await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}",
            new UpsertPeriodRequest(period.Name, period.StartDate, period.EndDate, period.LeaveDeadline, period.PointsBudget), ApiFixture.Json));
        Assert.Equal([left.Id], (await Read<List<PeriodDto>>(await admin.GetAsync("/api/admin/periods"))).Single(x => x.Id == kept.Id).ExcludedPersonIds);
        await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}",
            new UpsertPeriodRequest(period.Name, period.StartDate, period.EndDate, period.LeaveDeadline, period.PointsBudget, []), ApiFixture.Json);
        Assert.Empty((await Read<List<PeriodDto>>(await admin.GetAsync("/api/admin/periods"))).Single(x => x.Id == period.Id).ExcludedPersonIds!);
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

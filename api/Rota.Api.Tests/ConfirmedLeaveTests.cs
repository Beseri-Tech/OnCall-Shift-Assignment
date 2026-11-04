using System.Net;
using System.Net.Http.Json;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>Confirmed leave: approved absences the admin enters; never refused, but they shrink the officer's own allowance.</summary>
public class ConfirmedLeaveTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly DateOnly Oct16 = new(2026, 10, 16), Oct17 = new(2026, 10, 17), Oct18 = new(2026, 10, 18);

    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private static async Task<(List<AdminPersonDto> People, PeriodDto Period)> SeedAsync(HttpClient admin, int people)
    {
        var names = string.Join("\n", Enumerable.Range(1, people).Select(i => $"Dr Test {i}"));
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names })).EnsureSuccessStatusCode();
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Oct 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null), ApiFixture.Json));
        return (await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people")), period);
    }

    private static Task<HttpResponseMessage> Confirm(HttpClient admin, Guid personId, DateOnly from, DateOnly to,
        string? reason = "Overseas trip, approved by HOD", string? reference = null) =>
        admin.PostAsJsonAsync("/api/admin/leave-approvals",
            new UpsertLeaveApprovalRequest(personId, from, to, reason, reference), ApiFixture.Json);

    private static Task<HttpResponseMessage> Save(HttpClient client, Guid personId, Guid periodId, params DateOnly[] days) =>
        client.PutAsJsonAsync($"/api/people/{personId}/entries?periodId={periodId}",
            new EntriesDto(days.Select(d => new LeaveEntryDto(d, null)).ToList(), []), ApiFixture.Json);

    [Fact]
    public async Task Confirmed_leave_is_never_refused_shrinks_the_allowance_and_fills_the_day_for_others()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin, 10);   // Oct 2026: 3 points; at most 3 of 10 off on a weekend
        var officers = new List<HttpClient>();
        foreach (var p in people) officers.Add(await ApiFixture.OfficerClientAsync(app, admin, p.Id));

        // Sat 17 Oct is full before the trip is approved.
        foreach (var who in new[] { 1, 2, 3 }) (await Save(officers[who], people[who].Id, period.Id, Oct17)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(officers[4], people[4].Id, period.Id, Oct17)).StatusCode);

        // The admin can still confirm it; nobody's leave is removed and the admin is told the day is over the limit.
        var result = await Read<LeaveApprovalResultDto>(await Confirm(admin, people[4].Id, Oct16, Oct18, reference: "MEMO 12/2026"));
        Assert.Equal(("Overseas trip, approved by HOD", "MEMO 12/2026"), (result.Approval.Reason, result.Approval.Reference));
        Assert.Contains(result.Warnings, w => w.Contains("Sat 17 Oct now has 4 of 10 officers off (limit 3)"));
        Assert.Empty(result.Approval.Conflicts);

        // Even a short trip (Fri 16 to Sun 18: 2 points of busy days, 1 weekday) takes one point away because the share rounds up: 2 left.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Save(officers[4], people[4].Id, period.Id, new(2026, 10, 3), new(2026, 10, 4), new(2026, 10, 10))).StatusCode);
        (await Save(officers[4], people[4].Id, period.Id, new(2026, 10, 3), new(2026, 10, 4))).EnsureSuccessStatusCode();
        var points = await Read<List<PointsRowDto>>(await admin.GetAsync($"/api/admin/periods/{period.Id}/points"));
        Assert.Equal(2, points.Single(p => p.PersonId == people[4].Id).PointsUsed);

        // Saving their own leave doesn't touch the confirmed days, which come back separately.
        var entries = await Read<EntriesDto>(await officers[4].GetAsync($"/api/people/{people[4].Id}/entries?periodId={period.Id}"));
        Assert.Equal(2, entries.Leave.Count);
        Assert.Equal((Oct16, Oct18), entries.Confirmed!.Select(c => (c.StartDate, c.EndDate)).Single());

        // Confirmed days count for everyone else: when one officer cancels, the day stays full (2 + the trip = 3).
        (await Save(officers[1], people[1].Id, period.Id)).EnsureSuccessStatusCode();
        var full = await Save(officers[5], people[5].Id, period.Id, Oct17);
        Assert.Equal(HttpStatusCode.BadRequest, full.StatusCode);
        Assert.Contains("Already full", await full.Content.ReadAsStringAsync());
        var rules = await Read<LeaveRulesDto>(await officers[5].GetAsync($"/api/periods/{period.Id}/leave-rules?personId={people[5].Id}"));
        Assert.Equal(3, rules.OthersOff[Oct17]);

        // Other officers see "confirmed" without the reason; the officer sees the reason.
        var theirs = await Read<OverviewDto>(await officers[5].GetAsync($"/api/periods/{period.Id}/overview"));
        var seen = theirs.People.Single(p => p.Id == people[4].Id).Leave.Single(l => l.Date == Oct17);
        Assert.Equal((true, (string?)null), (seen.Confirmed, seen.Note));
        var own = await Read<OverviewDto>(await officers[4].GetAsync($"/api/periods/{period.Id}/overview"));
        Assert.Equal("Overseas trip, approved by HOD", own.People.Single(p => p.Id == people[4].Id).Leave.Single(l => l.Date == Oct17).Note);

        // A longer trip takes 33% of what its days would have cost, rounded up. Sat 3 to Sun 11 Oct has 4 weekend days
        // (4 points) and 5 weekdays: 3 points - 2 = 1, 11 weekdays - 2 = 9. The trip itself is never refused.
        var longTrip = await Read<LeaveApprovalResultDto>(await Confirm(admin, people[6].Id, new(2026, 10, 3), new(2026, 10, 11)));
        Assert.Contains(longTrip.Warnings, w => w.Contains("covers 4 point(s)") && w.Contains("and 5 weekday(s); 33% of that is taken off, so points are 3 - 2 = 1 and weekdays 11 - 2 = 9"));
        var over = await Save(officers[6], people[6].Id, period.Id, new(2026, 10, 24), new(2026, 10, 25));
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("Not enough points: 2 needed, 1 available", await over.Content.ReadAsStringAsync());
        (await Save(officers[6], people[6].Id, period.Id, new DateOnly(2026, 10, 24))).EnsureSuccessStatusCode();
        var rules6 = await Read<LeaveRulesDto>(await officers[6].GetAsync($"/api/periods/{period.Id}/leave-rules?personId={people[6].Id}"));
        Assert.Equal((9, 31, 2, 2), (rules6.ConfirmedDays, rules6.PeriodDays, rules6.PointsDeducted, rules6.WeekdaysDeducted));
        var row6 = (await Read<List<PointsRowDto>>(await admin.GetAsync($"/api/admin/periods/{period.Id}/points"))).Single(p => p.PersonId == people[6].Id);
        Assert.Equal((1, 2, 2), (row6.PointsUsed, row6.PointsDeducted, row6.WeekdaysDeducted));

        // Officers can't use the admin endpoints.
        Assert.Equal(HttpStatusCode.Forbidden, (await Confirm(officers[4], people[4].Id, Oct16, Oct18)).StatusCode);

        // The rota never puts them on call during the trip.
        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        Assert.DoesNotContain(run.Days, d => d.PersonId == people[4].Id && d.Date >= Oct16 && d.Date <= Oct18);
    }

    [Fact]
    public async Task Confirmed_leave_needs_a_reason_does_not_overlap_and_takes_over_leave_already_marked()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin, 6);
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Confirm(admin, people[0].Id, Oct16, Oct18, reason: " ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Confirm(admin, people[0].Id, Oct18, Oct16)).StatusCode);

        // Leave the officer marked on the trip days becomes confirmed and stops using points; a preferred day there goes.
        (await officer.PutAsJsonAsync($"/api/people/{people[0].Id}/entries?periodId={period.Id}",
            new EntriesDto([new(Oct17, "Annual")], [Oct16]), ApiFixture.Json)).EnsureSuccessStatusCode();
        var result = await Read<LeaveApprovalResultDto>(await Confirm(admin, people[0].Id, Oct16, Oct18));
        Assert.Contains(result.Warnings, w => w.Contains("1 day(s)") && w.Contains("now confirmed leave"));
        Assert.Contains(result.Warnings, w => w.Contains("1 preferred on-call day(s)"));
        var entries = await Read<EntriesDto>(await officer.GetAsync($"/api/people/{people[0].Id}/entries?periodId={period.Id}"));
        Assert.Empty(entries.Leave);
        Assert.Empty(entries.Preferred);

        var overlap = await Confirm(admin, people[0].Id, Oct18, new DateOnly(2026, 10, 20));
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Contains("already has confirmed leave", await overlap.Content.ReadAsStringAsync());

        // Shortening it frees the other days; deleting it removes the leave and tells the officer.
        var id = result.Approval.Id;
        (await admin.PutAsJsonAsync($"/api/admin/leave-approvals/{id}",
            new UpsertLeaveApprovalRequest(people[0].Id, Oct16, Oct16, "Course, approved", null), ApiFixture.Json)).EnsureSuccessStatusCode();
        var list = await Read<List<LeaveApprovalDto>>(await admin.GetAsync($"/api/admin/leave-approvals?personId={people[0].Id}"));
        Assert.Equal((Oct16, Oct16, "Course, approved"), list.Select(a => (a.StartDate, a.EndDate, a.Reason)).Single());
        var overview = await Read<OverviewDto>(await admin.GetAsync($"/api/periods/{period.Id}/overview"));
        Assert.Equal([Oct16], overview.People.Single(p => p.Id == people[0].Id).Leave.Select(l => l.Date));

        (await admin.DeleteAsync($"/api/admin/leave-approvals/{id}")).EnsureSuccessStatusCode();
        overview = await Read<OverviewDto>(await admin.GetAsync($"/api/periods/{period.Id}/overview"));
        Assert.Empty(overview.People.Single(p => p.Id == people[0].Id).Leave);

        var notes = await Read<NotificationsDto>(await officer.GetAsync("/api/notifications"));
        Assert.Contains(notes.Items, n => n.Title.StartsWith("Confirmed leave added"));
        Assert.Contains(notes.Items, n => n.Title.StartsWith("Confirmed leave changed"));
        Assert.Contains(notes.Items, n => n.Title.StartsWith("Confirmed leave removed"));
        var log = await admin.GetStringAsync("/api/admin/audit?entity=confirmed-leave");
        Assert.Contains("confirmed-leave.create", log);
        Assert.Contains("confirmed-leave.delete", log);
    }

    [Fact]
    public async Task Confirmed_leave_can_be_entered_before_the_period_exists()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, _) = await SeedAsync(admin, 6);
        DateOnly nov2 = new(2026, 11, 2), nov6 = new(2026, 11, 6);

        var result = await Read<LeaveApprovalResultDto>(await Confirm(admin, people[0].Id, nov2, nov6));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("officers off"));

        var nov = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Nov 2026", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null), ApiFixture.Json));
        var entries = await Read<EntriesDto>(await admin.GetAsync($"/api/people/{people[0].Id}/entries?periodId={nov.Id}"));
        Assert.Equal((nov2, nov6), entries.Confirmed!.Select(c => (c.StartDate, c.EndDate)).Single());
        var rules = await Read<LeaveRulesDto>(await admin.GetAsync($"/api/periods/{nov.Id}/leave-rules?personId={people[1].Id}"));
        Assert.Equal(1, rules.OthersOff[nov2]);
    }

    [Fact]
    public async Task Late_approval_lists_the_shifts_to_hand_over_with_who_is_free()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var (people, period) = await SeedAsync(admin, 6);

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(2)));
        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();

        var shift = run.Days.First(d => d.PersonId is not null && d.Date.Day > 1 && d.Date.Day < 31);
        var who = shift.PersonId!.Value;
        var result = await Read<LeaveApprovalResultDto>(await Confirm(admin, who, shift.Date, shift.Date));

        var conflict = Assert.Single(result.Approval.Conflicts);
        Assert.Equal((run.Run.Id, shift.Date, true), (conflict.RunId, conflict.Date, conflict.IsPublished));
        Assert.Contains(result.Warnings, w => w.Contains("Give those shifts to someone else"));

        // Candidates: not the officer, nobody working the day before, the day or the day after; fewest shifts first.
        var busy = run.Days.Where(d => Math.Abs(d.Date.DayNumber - shift.Date.DayNumber) <= 1).Select(d => d.PersonId).ToHashSet();
        Assert.NotEmpty(conflict.Candidates);
        Assert.All(conflict.Candidates, c => Assert.DoesNotContain(c.PersonId, busy));
        Assert.Equal(conflict.Candidates.OrderBy(c => c.Shifts).Select(c => c.Shifts), conflict.Candidates.Select(c => c.Shifts));
    }
}

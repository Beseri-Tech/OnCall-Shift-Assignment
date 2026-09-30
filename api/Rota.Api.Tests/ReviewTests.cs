using System.Net;
using System.Net.Http.Json;
using Rota.Api.Data;

namespace Rota.Api.Tests;

public class ReviewTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Officers_swap_dates_while_the_rota_is_in_review_and_are_notified()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr A\nDr B\nDr C\nDr D" })).EnsureSuccessStatusCode();
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        Guid a = people[0].Id, b = people[1].Id, c = people[2].Id, d = people[3].Id;
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Oct 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null), ApiFixture.Json));
        var officerA = await ApiFixture.OfficerClientAsync(app, admin, a);
        var officerB = await ApiFixture.OfficerClientAsync(app, admin, b);

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));
        (await admin.PostAsJsonAsync($"/api/admin/runs/{run.Run.Id}/review", new ReviewRequest(null))).EnsureSuccessStatusCode();

        // Everyone is told; officers see their draft dates; nothing counts yet.
        Assert.Contains((await Read<NotificationsDto>(await officerA.GetAsync("/api/notifications"))).Items,
            n => n.Kind == "review" && n.Link == "/my-oncall");
        var rota = await Read<PublishedRotaDto>(await officerA.GetAsync($"/api/periods/{period.Id}/rota"));
        Assert.True(rota.InReview);
        Assert.All(await Read<List<ShiftDto>>(await officerA.GetAsync($"/api/people/{a}/shifts")), s => Assert.True(s.InReview));
        Assert.All(await Read<List<TallyRowDto>>(await admin.GetAsync("/api/admin/tally")), r => Assert.Equal(0, r.Total));

        DateOnly DayOf(Guid person, int nth = 0) => rota.Days.Where(x => x.PersonId == person).Select(x => x.Date).ElementAt(nth);
        Task<HttpResponseMessage> Ask(HttpClient who, DateOnly from, DateOnly to) =>
            who.PostAsJsonAsync("/api/swaps", new CreateSwapRequest(run.Run.Id, from, to, "family dinner"));

        // A asks B; B accepts; the rota updates immediately.
        DateOnly x = DayOf(a), y = DayOf(b);
        var swap = await Read<SwapDto>(await Ask(officerA, x, y));
        Assert.Equal((b, SwapStatus.Pending), (swap.ToPersonId, swap.Status));
        Assert.Equal(HttpStatusCode.Forbidden, (await officerA.PostAsync($"/api/swaps/{swap.Id}/accept", null)).StatusCode);
        Assert.Contains((await Read<NotificationsDto>(await officerB.GetAsync("/api/notifications"))).Items, n => n.Kind == "swap-request");
        (await officerB.PostAsync($"/api/swaps/{swap.Id}/accept", null)).EnsureSuccessStatusCode();

        rota = await Read<PublishedRotaDto>(await officerA.GetAsync($"/api/periods/{period.Id}/rota"));
        Assert.Equal(b, rota.Days.Single(z => z.Date == x).PersonId);
        Assert.Equal(a, rota.Days.Single(z => z.Date == y).PersonId);
        Assert.Contains((await Read<NotificationsDto>(await officerA.GetAsync("/api/notifications"))).Items, n => n.Kind == "swap-accepted");
        Assert.Equal(SwapStatus.Accepted, (await Read<List<SwapDto>>(await officerB.GetAsync("/api/swaps"))).Single(s => s.Id == swap.Id).Status);
        Assert.DoesNotContain((await Read<NotificationsDto>(await officerA.GetAsync("/api/notifications"))).Items, n => n.Kind == "swap-cancelled");

        // No swapping onto your own leave (admins can enter leave after the lock).
        DateOnly bDay = DayOf(b, 1), aDay = DayOf(a, 1);
        (await admin.PutAsJsonAsync($"/api/people/{b}/entries?periodId={period.Id}",
            new EntriesDto([new(aDay, "Course")], []), ApiFixture.Json)).EnsureSuccessStatusCode();
        var clash = await Ask(officerA, aDay, bDay);
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        Assert.Contains("on leave", await clash.Content.ReadAsStringAsync());

        // A pending request closes when the admin changes one of its days.
        DateOnly cDay = DayOf(c), aDay2 = DayOf(a, 2);
        var stale = await Read<SwapDto>(await Ask(officerA, aDay2, cDay));
        (await admin.PutAsJsonAsync($"/api/admin/runs/{run.Run.Id}/assignments/{cDay:yyyy-MM-dd}", new OverrideRequest(d)))
            .EnsureSuccessStatusCode();
        var mine = await Read<List<SwapDto>>(await officerA.GetAsync("/api/swaps"));
        Assert.Equal(SwapStatus.Expired, mine.Single(s => s.Id == stale.Id).Status);

        // Publishing ends the review: shifts are final and count in the tally.
        var open = await Read<SwapDto>(await Ask(officerA, DayOf(a, 3), DayOf(b, 2)));
        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();
        Assert.Equal(SwapStatus.Expired, (await Read<List<SwapDto>>(await officerA.GetAsync("/api/swaps"))).Single(s => s.Id == open.Id).Status);
        Assert.All(await Read<List<ShiftDto>>(await officerA.GetAsync($"/api/people/{a}/shifts")), s => Assert.False(s.InReview));
        Assert.Equal(31, (await Read<List<TallyRowDto>>(await admin.GetAsync("/api/admin/tally"))).Sum(r => r.Total));
        Assert.Equal(HttpStatusCode.BadRequest, (await Ask(officerB, DayOf(b), DayOf(a))).StatusCode);

        var bell = await Read<NotificationsDto>(await officerB.GetAsync("/api/notifications"));
        Assert.Contains(bell.Items, n => n.Kind == "published");
        Assert.True(bell.Unread > 0);
        (await officerB.PostAsync("/api/notifications/read-all", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await Read<NotificationsDto>(await officerB.GetAsync("/api/notifications"))).Unread);
    }

    [Fact]
    public async Task No_swaps_after_the_swap_deadline()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr A\nDr B" })).EnsureSuccessStatusCode();
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Oct 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null), ApiFixture.Json));
        var officerA = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(1)));

        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-2));
        (await admin.PostAsJsonAsync($"/api/admin/runs/{run.Run.Id}/review", new ReviewRequest(yesterday))).EnsureSuccessStatusCode();
        DateOnly mine = run.Days.First(x => x.PersonId == people[0].Id).Date, theirs = run.Days.First(x => x.PersonId == people[1].Id).Date;

        var late = await officerA.PostAsJsonAsync("/api/swaps", new CreateSwapRequest(run.Run.Id, mine, theirs, null));
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        Assert.Contains("Swaps closed", await late.Content.ReadAsStringAsync());
    }
}

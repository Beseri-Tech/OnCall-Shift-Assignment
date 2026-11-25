using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Tests;

/// <summary>A period can be set to open to officers at a later time: notices, no early leave, then it opens by itself.</summary>
public class ScheduledPeriodTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task A_scheduled_period_stays_closed_until_its_time_then_opens_once()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        await ApiFixture.AddPeopleAsync(admin, "Dr One\nDr Two");
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);

        var opensAt = DateTimeOffset.UtcNow.AddHours(30);
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Dec 2026", new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 31), null, OpensAt: opensAt), ApiFixture.Json));
        Assert.False(period.IsEditable);
        Assert.NotNull(period.OpensAt);

        // Officers can't save leave before it opens, and are told when it will.
        var url = $"/api/people/{people[0].Id}/entries?periodId={period.Id}";
        var days = new EntriesDto([new LeaveEntryDto(new DateOnly(2026, 12, 5), null)], []);
        Assert.Equal(HttpStatusCode.Conflict, (await officer.PutAsJsonAsync(url, days, ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(url, days, ApiFixture.Json)).StatusCode);   // the admin still can

        var opener = app.Services.GetRequiredService<PeriodOpener>();
        Assert.Equal(0, await opener.RunOnceAsync(CancellationToken.None));
        Assert.Equal(1, await CountAsync(app, "opens on"));

        // Within a day: one reminder, not repeated.
        await SetOpensAtAsync(app, period.Id, DateTimeOffset.UtcNow.AddHours(5));
        await opener.RunOnceAsync(CancellationToken.None);
        await opener.RunOnceAsync(CancellationToken.None);
        Assert.Equal(1, await CountAsync(app, "opens tomorrow"));

        // At the time: it opens, officers are told once, and leave can be saved.
        await SetOpensAtAsync(app, period.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(1, await opener.RunOnceAsync(CancellationToken.None));
        Assert.Equal(0, await opener.RunOnceAsync(CancellationToken.None));
        Assert.Equal(1, await CountAsync(app, "Enter your leave for Dec 2026"));
        Assert.Equal(HttpStatusCode.OK, (await officer.PutAsJsonAsync(url, days, ApiFixture.Json)).StatusCode);
    }

    [Fact]
    public async Task Rescheduling_tells_officers_and_open_now_opens_it()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        await ApiFixture.AddPeopleAsync(admin, "Dr One");
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);

        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Dec 2026", new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 31), null, OpensAt: DateTimeOffset.UtcNow.AddDays(3)),
            ApiFixture.Json));

        var later = DateTimeOffset.UtcNow.AddDays(4);
        var moved = await Read<PeriodDto>(await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}",
            new UpsertPeriodRequest(period.Name, period.StartDate, period.EndDate, null, OpensAt: later), ApiFixture.Json));
        Assert.Equal(later.ToUnixTimeSeconds(), moved.OpensAt!.Value.ToUnixTimeSeconds());
        Assert.Equal(1, await CountAsync(app, "now opens on"));

        var open = await Read<PeriodDto>(await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}",
            new UpsertPeriodRequest(period.Name, period.StartDate, period.EndDate, null, OpensAt: null), ApiFixture.Json));
        Assert.Null(open.OpensAt);
        Assert.True(open.IsEditable);

        // Opening after the deadline makes no sense.
        var bad = await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Mar 2027", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31), new DateOnly(2027, 2, 1),
                OpensAt: new DateTimeOffset(2027, 2, 10, 12, 0, 0, TimeSpan.Zero)), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    private static async Task<int> CountAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app, string titlePart)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RotaDbContext>().Notifications
            .CountAsync(n => n.Kind == Notifier.PeriodOpen && n.Title.Contains(titlePart));
    }

    private static async Task SetOpensAtAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app, Guid periodId, DateTimeOffset at)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();
        var p = await db.Periods.SingleAsync(x => x.Id == periodId);
        p.OpensAt = at;
        await db.SaveChangesAsync();
    }
}

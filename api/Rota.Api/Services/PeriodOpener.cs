using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Endpoints;

namespace Rota.Api.Services;

/// <summary>
/// Opens scheduled rota periods (<see cref="RotaPeriod.OpensAt"/>): a day and an hour before it sends a reminder, and when
/// the time comes tells officers they can enter leave. Leave entry itself is gated by <c>HasOpened</c> on every request,
/// so a late or missed run only delays the notices, never the opening. Checks every 30 seconds.
/// </summary>
public sealed class PeriodOpener(IServiceScopeFactory scopes, TimeProvider time, ILogger<PeriodOpener> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "Opening scheduled periods failed; trying again shortly.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>One check; returns how many periods had their opening notice sent. Public so tests can run it on demand.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();   // no signed-in hospital here: sees every hospital
        var notify = scope.ServiceProvider.GetRequiredService<Notifier>();
        var clock = scope.ServiceProvider.GetRequiredService<LocalClock>();

        var periods = await db.Periods.Where(p => p.OpensAt != null && p.OpenNoticeSentAt == null).ToListAsync(ct);
        int opened = 0;
        foreach (var p in periods)
        {
            var excluded = await db.PeriodExclusions.Where(x => x.PeriodId == p.Id).Select(x => x.PersonId).ToListAsync(ct);
            string range = $"{SwapEndpoints.Day(p.StartDate)} - {SwapEndpoints.Day(p.EndDate)}";
            string deadline = p.LeaveDeadline is { } d ? $" until {SwapEndpoints.Day(d)}" : "";
            var left = p.OpensAt!.Value - now;

            if (left <= TimeSpan.Zero)
            {
                await notify.ToOnCallOfficersAsync(Notifier.PeriodOpen, $"Enter your leave for {p.Name}",
                    $"Leave for {range} is open now{deadline}.", "/", email: true, ct, excluded, p.FacilityId);
                p.OpenNoticeSentAt = now;
                opened++;
            }
            else if (left <= TimeSpan.FromHours(1) && p.HourReminderSentAt is null)
            {
                await notify.ToOnCallOfficersAsync(Notifier.PeriodOpen, $"{p.Name} opens in about an hour",
                    $"Leave for {range} opens on {clock.Format(p.OpensAt.Value)}.", "/", email: false, ct, excluded, p.FacilityId);
                p.HourReminderSentAt = p.DayReminderSentAt = now;
            }
            else if (left <= TimeSpan.FromHours(24) && p.DayReminderSentAt is null)
            {
                await notify.ToOnCallOfficersAsync(Notifier.PeriodOpen, $"{p.Name} opens tomorrow",
                    $"Leave for {range} opens on {clock.Format(p.OpensAt.Value)}.", "/", email: true, ct, excluded, p.FacilityId);
                p.DayReminderSentAt = now;
            }
        }
        await db.SaveChangesAsync(ct);
        if (opened > 0) log.LogInformation("Opened {Count} scheduled period(s).", opened);
        return opened;
    }
}

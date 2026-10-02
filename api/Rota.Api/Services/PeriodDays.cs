using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Services;

/// <summary>Keeps a period's own holidays and peak days in step with its dates and the master lists.</summary>
public static class PeriodDays
{
    /// <summary>
    /// Adds master-list holidays and peak days dated <paramref name="from"/>-<paramref name="to"/> that the period
    /// doesn't have yet. Returns how many were added (not saved).
    /// </summary>
    public static async Task<int> CopyFromMasterAsync(RotaDbContext db, RotaPeriod period, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        var have = (await db.PeriodDays.Where(d => d.PeriodId == period.Id).Select(d => new { d.Kind, d.Date }).ToListAsync(ct))
            .Select(d => (d.Kind, d.Date)).ToHashSet();
        var master = (await db.Holidays.Where(h => h.Date >= from && h.Date <= to)
                .Select(h => new { h.Date, h.Name }).ToListAsync(ct))
            .Select(h => (Kind: PeriodDayKind.Holiday, h.Date, h.Name))
            .Concat((await db.PeakDays.Where(p => p.Date >= from && p.Date <= to)
                .Select(p => new { p.Date, p.Name }).ToListAsync(ct))
                .Select(p => (Kind: PeriodDayKind.Peak, p.Date, p.Name)));

        int added = 0;
        foreach (var m in master.Where(m => !have.Contains((m.Kind, m.Date))))
        {
            db.PeriodDays.Add(new PeriodDay { PeriodId = period.Id, Date = m.Date, Kind = m.Kind, Name = m.Name });
            added++;
        }
        return added;
    }

    /// <summary>
    /// After a period's dates change: drops its days outside the new dates and adds master-list days for dates it
    /// didn't cover before (days it already covered keep the admin's choices). Not saved.
    /// </summary>
    public static async Task MoveAsync(RotaDbContext db, RotaPeriod period, DateOnly oldStart, DateOnly oldEnd, CancellationToken ct)
    {
        var outside = await db.PeriodDays
            .Where(d => d.PeriodId == period.Id && (d.Date < period.StartDate || d.Date > period.EndDate)).ToListAsync(ct);
        db.PeriodDays.RemoveRange(outside);

        if (period.StartDate < oldStart)
            await CopyFromMasterAsync(db, period, period.StartDate, Min(oldStart.AddDays(-1), period.EndDate), ct);
        if (period.EndDate > oldEnd)
            await CopyFromMasterAsync(db, period, Max(oldEnd.AddDays(1), period.StartDate), period.EndDate, ct);
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}

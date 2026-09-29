using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Endpoints;
using Rota.Core.Leave;

namespace Rota.Api.Services;

/// <summary>Everything the leave limits need for one period: the policy, budgets, grants and who is off when.</summary>
public sealed class LeaveRules
{
    public required LeavePolicy Policy { get; init; }
    public required RotaPeriod Period { get; init; }
    public required List<PeakDay> Peaks { get; init; }

    /// <summary>Leave dates in the period per on-call officer (officers without leave included).</summary>
    public required Dictionary<Guid, List<DateOnly>> LeaveByPerson { get; init; }

    public required Dictionary<Guid, PointGrant> Grants { get; init; }

    public int OnCall => LeaveByPerson.Count;
    public int BaseBudget => Period.PointsBudget ?? Policy.DefaultBudget;
    public int Budget(Guid personId) => BaseBudget + (Grants.GetValueOrDefault(personId)?.Points ?? 0);

    /// <summary>Per day, how many on-call officers other than <paramref name="personId"/> are off.</summary>
    public Dictionary<DateOnly, int> OthersOff(Guid personId) =>
        LeaveByPerson.Where(kv => kv.Key != personId).SelectMany(kv => kv.Value)
            .GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

    public List<string> Validate(Guid personId, IReadOnlyCollection<DateOnly> before, IReadOnlyCollection<DateOnly> after) =>
        Policy.Validate(before, after, OthersOff(personId), Budget(personId), OnCall);

    public static async Task<LeaveRules> LoadAsync(RotaDbContext db, LeaveLimits limits, RotaPeriod period, CancellationToken ct)
    {
        var holidays = (await Mapping.HolidaysAsync(db, period.StartDate, period.EndDate, ct)).Keys.ToHashSet();
        var peaks = await db.PeakDays.Where(p => p.Date >= period.StartDate && p.Date <= period.EndDate)
            .OrderBy(p => p.Date).ToListAsync(ct);

        var onCall = await db.People.Where(p => p.Status == OfficerStatus.OnCall).Select(p => p.Id).ToListAsync(ct);
        var leave = await db.LeaveDays
            .Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate)
            .Select(l => new { l.PersonId, l.Date }).ToListAsync(ct);
        var byPerson = onCall.ToDictionary(id => id, _ => new List<DateOnly>());
        foreach (var l in leave)
            if (byPerson.TryGetValue(l.PersonId, out var dates)) dates.Add(l.Date);

        return new LeaveRules
        {
            Policy = new LeavePolicy(limits, period.StartDate, period.EndDate, holidays, peaks.Select(p => p.Date).ToHashSet()),
            Period = period,
            Peaks = peaks,
            LeaveByPerson = byPerson,
            Grants = await db.PointGrants.Where(g => g.PeriodId == period.Id).ToDictionaryAsync(g => g.PersonId, ct),
        };
    }
}

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
    public required List<PeriodDay> Peaks { get; init; }

    /// <summary>Leave dates in the period per on-call officer (officers without leave included).</summary>
    public required Dictionary<Guid, List<DateOnly>> LeaveByPerson { get; init; }

    /// <summary>Confirmed-leave dates in the period per on-call officer (also included in <see cref="LeaveByPerson"/>).</summary>
    public required Dictionary<Guid, HashSet<DateOnly>> ConfirmedByPerson { get; init; }

    public required Dictionary<Guid, PointGrant> Grants { get; init; }

    /// <summary>The officer's own leave, which uses points and the weekday allowance: everything but confirmed leave.</summary>
    public List<DateOnly> OwnLeave(Guid personId)
    {
        var confirmed = ConfirmedByPerson.GetValueOrDefault(personId);
        return LeaveByPerson.GetValueOrDefault(personId, []).Where(d => confirmed?.Contains(d) != true).ToList();
    }

    /// <summary>Per day, how many on-call officers are off in total (confirmed leave included).</summary>
    public Dictionary<DateOnly, int> AllOff() =>
        LeaveByPerson.SelectMany(kv => kv.Value).GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

    public int OnCall => LeaveByPerson.Count;
    public int BaseBudget => Period.PointsBudget ?? Policy.DefaultBudget;
    public int Budget(Guid personId) => BaseBudget + (Grants.GetValueOrDefault(personId)?.Points ?? 0);

    /// <summary>
    /// Per day, how many on-call officers other than <paramref name="personId"/> are off. Confirmed leave counts too, so a
    /// day filled partly by approved trips is full for everyone else.
    /// </summary>
    public Dictionary<DateOnly, int> OthersOff(Guid personId) =>
        LeaveByPerson.Where(kv => kv.Key != personId).SelectMany(kv => kv.Value)
            .GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

    public int PeriodDays => Period.EndDate.DayNumber - Period.StartDate.DayNumber + 1;

    /// <summary>Days of confirmed leave the officer has in the period.</summary>
    public int ConfirmedDays(Guid personId) => ConfirmedByPerson.GetValueOrDefault(personId)?.Count ?? 0;

    /// <summary>
    /// Confirmed leave doesn't use points itself, but it takes away the same share of the officer's points and weekday
    /// allowance as the share of the period it covers: 15 days of a 60-day period removes 25% of each (rounded).
    /// </summary>
    public int PointsDeducted(Guid personId) => Share(Budget(personId), personId);

    public int WeekdaysDeducted(Guid personId) => Share(Policy.WeekdayAllowance, personId);

    private int Share(int amount, Guid personId) =>
        Math.Min(amount, (int)Math.Round(amount * (double)ConfirmedDays(personId) / PeriodDays, MidpointRounding.AwayFromZero));

    /// <summary>What the officer has for their own leave once confirmed leave is taken into account.</summary>
    public int OwnBudget(Guid personId) => Budget(personId) - PointsDeducted(personId);

    public int OwnWeekdayAllowance(Guid personId) => Policy.WeekdayAllowance - WeekdaysDeducted(personId);

    /// <summary>Checks a change to the officer's own leave; <paramref name="before"/> and <paramref name="after"/> exclude confirmed leave.</summary>
    public List<string> Validate(Guid personId, IReadOnlyCollection<DateOnly> before, IReadOnlyCollection<DateOnly> after)
    {
        var errors = Policy.Validate(before, after, OthersOff(personId), OwnBudget(personId), OnCall, OwnWeekdayAllowance(personId));
        if (errors.Count > 0 && ConfirmedDays(personId) > 0)
            errors.Add(DeductionText(personId, "Your"));
        return errors;
    }

    /// <summary>E.g. "Your confirmed leave covers 15 of 60 days (25%), so points are 5 - 1 = 4 and weekdays 21 - 5 = 16."</summary>
    public string DeductionText(Guid personId, string whose)
    {
        int days = ConfirmedDays(personId);
        return $"{whose} confirmed leave covers {days} of {PeriodDays} days ({(int)Math.Round(100.0 * days / PeriodDays)}%), " +
               $"so points are {Budget(personId)} - {PointsDeducted(personId)} = {OwnBudget(personId)} and weekdays " +
               $"{Policy.WeekdayAllowance} - {WeekdaysDeducted(personId)} = {OwnWeekdayAllowance(personId)}.";
    }

    public static async Task<LeaveRules> LoadAsync(RotaDbContext db, LeaveLimits limits, RotaPeriod period, CancellationToken ct)
    {
        var holidays = (await Mapping.HolidaysAsync(db, period.Id, ct)).Keys.ToHashSet();
        var peaks = await db.PeriodDays.Where(d => d.PeriodId == period.Id && d.Kind == PeriodDayKind.Peak)
            .OrderBy(d => d.Date).ToListAsync(ct);

        var onCall = await db.People.Where(p => p.Status == OfficerStatus.OnCall).Select(p => p.Id).ToListAsync(ct);
        var leave = await db.LeaveDays
            .Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate)
            .Select(l => new { l.PersonId, l.Date, l.ApprovalId }).ToListAsync(ct);
        var byPerson = onCall.ToDictionary(id => id, _ => new List<DateOnly>());
        var confirmed = onCall.ToDictionary(id => id, _ => new HashSet<DateOnly>());
        foreach (var l in leave)
        {
            if (!byPerson.TryGetValue(l.PersonId, out var dates)) continue;
            dates.Add(l.Date);
            if (l.ApprovalId is not null) confirmed[l.PersonId].Add(l.Date);
        }

        return new LeaveRules
        {
            Policy = new LeavePolicy(limits, period.StartDate, period.EndDate, holidays, peaks.Select(p => p.Date).ToHashSet()),
            Period = period,
            Peaks = peaks,
            LeaveByPerson = byPerson,
            ConfirmedByPerson = confirmed,
            Grants = await db.PointGrants.Where(g => g.PeriodId == period.Id).ToDictionaryAsync(g => g.PersonId, ct),
        };
    }
}

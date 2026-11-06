using System.Globalization;

namespace Rota.Core.Leave;

/// <summary>Limits on self-service leave; every officer does one shift per period, so leave only picks the day.</summary>
/// <param name="PointsPercent">Points per officer = this share of the period's weekend, public holiday and peak days.</param>
/// <param name="WeekdayPercent">Free weekday leave = this share of the period's ordinary weekdays.</param>
/// <param name="BusyDayCapPercent">At most this share of on-call officers off on the same weekend/public holiday/peak day.</param>
/// <param name="WeekdayCapPercent">At most this share of on-call officers off on the same ordinary weekday.</param>
/// <param name="HolidayReward">Bonus leave points, in the next rota, for working a public holiday.</param>
/// <param name="WeekendReward">Bonus leave points, in the next rota, for working a Saturday or Sunday.</param>
/// <param name="PeakReward">Bonus leave points, in the next rota, for working a peak day.</param>
/// <param name="ConfirmedLeavePercent">Confirmed leave takes this share of what its days would have cost as ordinary leave.</param>
public sealed record LeaveLimits(
    double PointsPercent = 0.30,
    int WeekendCost = 1,
    int HolidayCost = 2,
    int PeakCost = 1,
    double WeekdayPercent = 0.50,
    double BusyDayCapPercent = 0.30,
    double WeekdayCapPercent = 0.50,
    double ConfirmedLeavePercent = 0.33,
    int HolidayReward = 3,
    int WeekendReward = 2,
    int PeakReward = 1);

public enum DayKind { Weekday, Weekend, Holiday, Peak }

/// <summary>Leave rules for one period: weekend/holiday/peak days cost points, weekdays have a free allowance, busy days fill up.</summary>
public sealed class LeavePolicy(
    LeaveLimits limits, DateOnly start, DateOnly end, IReadOnlySet<DateOnly> holidays, IReadOnlySet<DateOnly> peaks)
{
    public LeaveLimits Limits => limits;

    /// <summary>A public holiday on a weekend is a Holiday; peak days only matter on weekdays.</summary>
    public DayKind Kind(DateOnly d) =>
        holidays.Contains(d) ? DayKind.Holiday
        : d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? DayKind.Weekend
        : peaks.Contains(d) ? DayKind.Peak
        : DayKind.Weekday;

    public int Cost(DateOnly d) => Kind(d) switch
    {
        DayKind.Weekend => limits.WeekendCost,
        DayKind.Holiday => limits.HolidayCost,
        DayKind.Peak => limits.PeakCost,
        _ => 0,
    };

    public int Points(IEnumerable<DateOnly> dates) => dates.Distinct().Sum(Cost);

    public int Weekdays(IEnumerable<DateOnly> dates) => dates.Distinct().Count(d => Kind(d) == DayKind.Weekday);

    /// <summary>
    /// Points taken off an officer's budget for confirmed leave: <see cref="LeaveLimits.ConfirmedLeavePercent"/> of what those
    /// days would have cost as ordinary leave (weekend/peak 1, public holiday 2), rounded up, never more than the budget.
    /// </summary>
    public int ConfirmedPointsDeduction(IEnumerable<DateOnly> confirmed, int budget) =>
        Math.Min(Math.Max(0, budget), CeilShare(Points(confirmed)));

    /// <summary>Weekdays taken off the weekday allowance: the same share of the ordinary weekdays in the confirmed leave.</summary>
    public int ConfirmedWeekdayDeduction(IEnumerable<DateOnly> confirmed, int allowance) =>
        Math.Min(Math.Max(0, allowance), CeilShare(Weekdays(confirmed)));

    private int CeilShare(int amount) => (int)Math.Ceiling(amount * limits.ConfirmedLeavePercent - 1e-9);

    private IEnumerable<DateOnly> Days()
    {
        for (var d = start; d <= end; d = d.AddDays(1)) yield return d;
    }

    public int DefaultBudget =>
        (int)Math.Round(Days().Count(d => Kind(d) != DayKind.Weekday) * limits.PointsPercent,
            MidpointRounding.AwayFromZero);

    public int WeekdayAllowance => (int)Math.Floor(Days().Count(d => Kind(d) == DayKind.Weekday) * limits.WeekdayPercent);

    /// <summary>How many officers may be off on <paramref name="d"/>: stricter on weekends, public holidays and peak days.</summary>
    public int DayCap(DateOnly d, int onCall) => Kind(d) == DayKind.Weekday ? WeekdayCap(onCall) : BusyDayCap(onCall);

    /// <summary>Officers available on <paramref name="d"/>: everyone on call minus those on confirmed leave that day.</summary>
    public static int Available(DateOnly d, int onCall, IReadOnlyDictionary<DateOnly, int>? confirmedOff) =>
        Math.Max(0, onCall - (confirmedOff?.GetValueOrDefault(d) ?? 0));

    public int BusyDayCap(int onCall) => Cap(onCall, limits.BusyDayCapPercent);
    public int WeekdayCap(int onCall) => Cap(onCall, limits.WeekdayCapPercent);
    private static int Cap(int onCall, double percent) => onCall <= 0 ? 0 : Math.Max(1, (int)Math.Floor(onCall * percent));

    /// <summary>
    /// Errors for replacing <paramref name="before"/> with <paramref name="after"/>. A save must end within the points budget
    /// and weekday allowance. The one exception is an officer who is already over (an admin edit, a lowered budget, a later
    /// holiday...): they may leave things as they are or remove days (fewer points / weekdays than before), but never swap or
    /// add days while over, so nobody can stay over by rearranging leave.
    /// </summary>
    /// <param name="othersOff">Per day, how many other on-call officers are already off on ordinary leave (confirmed leave is not counted here).</param>
    /// <param name="confirmedOff">Per day, how many on-call officers are away on confirmed leave. They are taken out of the base the daily limit is worked out on.</param>
    /// <param name="weekdayAllowance">Overrides <see cref="WeekdayAllowance"/> (e.g. reduced for confirmed leave).</param>
    public List<string> Validate(IReadOnlyCollection<DateOnly> before, IReadOnlyCollection<DateOnly> after,
        IReadOnlyDictionary<DateOnly, int> othersOff, int budget, int onCall, int? weekdayAllowance = null,
        IReadOnlyDictionary<DateOnly, int>? confirmedOff = null)
    {
        var errors = new List<string>();
        bool unchanged = before.ToHashSet().SetEquals(after);

        int points = Points(after), pointsBefore = Points(before);
        if (points > budget && !unchanged && points >= pointsBefore)
            errors.Add(points > pointsBefore || pointsBefore <= budget
                ? $"Not enough points: {points} needed, {budget} available. Ask the admin for extra points."
                : $"You are over your points ({points} of {budget}). You can only remove days until you are within budget.");

        int weekdays = Weekdays(after), weekdaysBefore = Weekdays(before), allowance = weekdayAllowance ?? WeekdayAllowance;
        if (weekdays > allowance && !unchanged && weekdays >= weekdaysBefore)
            errors.Add(weekdays > weekdaysBefore || weekdaysBefore <= allowance
                ? $"Too many weekdays: {weekdays} marked, at most {allowance} in this period."
                : $"You are over your weekdays ({weekdays} of {allowance}). You can only remove days until you are within the allowance.");

        var full = after.Except(before).Distinct().Where(d => othersOff.GetValueOrDefault(d) >= DayCap(d, Available(d, onCall, confirmedOff))).Order().ToList();
        if (full.Count > 0)
            errors.Add($"Already full (at most {BusyDayCap(onCall)} of {onCall} officers off on a weekend/public holiday/peak day, " +
                       $"{WeekdayCap(onCall)} on other days, counting only officers not on confirmed leave that day): " +
                       string.Join(", ", full.Select(d => d.ToString("d MMM", CultureInfo.InvariantCulture))) + ".");

        return errors;
    }
}

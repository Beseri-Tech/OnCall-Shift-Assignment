using System.Globalization;

namespace Rota.Core.Leave;

/// <summary>Limits on self-service leave; every officer does one shift per period, so leave only picks the day.</summary>
/// <param name="PointsPercent">Points per officer = this share of the period's weekend, public holiday and peak days.</param>
/// <param name="WeekdayPercent">Free weekday leave = this share of the period's ordinary weekdays.</param>
/// <param name="BusyDayCapPercent">At most this share of on-call officers off on the same weekend/public holiday/peak day.</param>
/// <param name="WeekdayCapPercent">At most this share of on-call officers off on the same ordinary weekday.</param>
public sealed record LeaveLimits(
    double PointsPercent = 0.30,
    int WeekendCost = 1,
    int HolidayCost = 2,
    int PeakCost = 1,
    double WeekdayPercent = 0.50,
    double BusyDayCapPercent = 0.30,
    double WeekdayCapPercent = 0.50);

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

    public int BusyDayCap(int onCall) => Cap(onCall, limits.BusyDayCapPercent);
    public int WeekdayCap(int onCall) => Cap(onCall, limits.WeekdayCapPercent);
    private static int Cap(int onCall, double percent) => Math.Max(1, (int)Math.Floor(onCall * percent));

    /// <summary>
    /// Errors for replacing <paramref name="before"/> with <paramref name="after"/>. A save must end within the points budget
    /// and weekday allowance. The one exception is an officer who is already over (an admin edit, a lowered budget, a later
    /// holiday...): they may leave things as they are or remove days (fewer points / weekdays than before), but never swap or
    /// add days while over, so nobody can stay over by rearranging leave.
    /// </summary>
    /// <param name="othersOff">Per day, how many other on-call officers are already off.</param>
    /// <param name="weekdayAllowance">Overrides <see cref="WeekdayAllowance"/> (e.g. reduced for confirmed leave).</param>
    public List<string> Validate(IReadOnlyCollection<DateOnly> before, IReadOnlyCollection<DateOnly> after,
        IReadOnlyDictionary<DateOnly, int> othersOff, int budget, int onCall, int? weekdayAllowance = null)
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

        var full = after.Except(before).Distinct().Where(d => othersOff.GetValueOrDefault(d) >= DayCap(d, onCall)).Order().ToList();
        if (full.Count > 0)
            errors.Add($"Already full (at most {BusyDayCap(onCall)} of {onCall} officers off on a weekend/public holiday/peak day, " +
                       $"{WeekdayCap(onCall)} on other days): " +
                       string.Join(", ", full.Select(d => d.ToString("d MMM", CultureInfo.InvariantCulture))) + ".");

        return errors;
    }
}

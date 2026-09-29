using System.Globalization;

namespace Rota.Core.Leave;

/// <summary>Limits on self-service leave; every officer does one shift per period, so leave only picks the day.</summary>
/// <param name="PointsPercent">Points per officer = this share of the period's weekend/public holiday days.</param>
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
        (int)Math.Round(Days().Count(d => Kind(d) is DayKind.Weekend or DayKind.Holiday) * limits.PointsPercent,
            MidpointRounding.AwayFromZero);

    public int WeekdayAllowance => (int)Math.Floor(Days().Count(d => Kind(d) == DayKind.Weekday) * limits.WeekdayPercent);

    /// <summary>How many officers may be off on <paramref name="d"/>: stricter on weekends, public holidays and peak days.</summary>
    public int DayCap(DateOnly d, int onCall) => Kind(d) == DayKind.Weekday ? WeekdayCap(onCall) : BusyDayCap(onCall);

    public int BusyDayCap(int onCall) => Cap(onCall, limits.BusyDayCapPercent);
    public int WeekdayCap(int onCall) => Cap(onCall, limits.WeekdayCapPercent);
    private static int Cap(int onCall, double percent) => Math.Max(1, (int)Math.Floor(onCall * percent));

    /// <summary>
    /// Errors for replacing <paramref name="before"/> with <paramref name="after"/>. Only changes that make things worse
    /// are refused, so lowering a budget never stops anyone from editing or removing leave.
    /// </summary>
    /// <param name="othersOff">Per day, how many other on-call officers are already off.</param>
    public List<string> Validate(IReadOnlyCollection<DateOnly> before, IReadOnlyCollection<DateOnly> after,
        IReadOnlyDictionary<DateOnly, int> othersOff, int budget, int onCall)
    {
        var errors = new List<string>();

        int points = Points(after);
        if (points > budget && points > Points(before))
            errors.Add($"Not enough points: {points} needed, {budget} available. Ask the admin for extra points.");

        int weekdays = Weekdays(after), allowance = WeekdayAllowance;
        if (weekdays > allowance && weekdays > Weekdays(before))
            errors.Add($"Too many weekdays: {weekdays} marked, at most {allowance} in this period.");

        var full = after.Except(before).Distinct().Where(d => othersOff.GetValueOrDefault(d) >= DayCap(d, onCall)).Order().ToList();
        if (full.Count > 0)
            errors.Add($"Already full (at most {BusyDayCap(onCall)} of {onCall} officers off on a weekend/public holiday/peak day, " +
                       $"{WeekdayCap(onCall)} on other days): " +
                       string.Join(", ", full.Select(d => d.ToString("d MMM", CultureInfo.InvariantCulture))) + ".");

        return errors;
    }
}

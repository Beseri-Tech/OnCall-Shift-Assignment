namespace Rota.Core.Leave;

/// <summary>
/// Weekdays an officer can never be on call, stored as a bitmask (bit n = <see cref="DayOfWeek"/> n). They behave like leave for the
/// rota, but cost no points and don't count toward anyone's daily limit.
/// </summary>
public static class BlockedDays
{
    public static int ToMask(IEnumerable<int>? days) =>
        (days ?? []).Where(d => d is >= 0 and <= 6).Distinct().Aggregate(0, (mask, d) => mask | (1 << d));

    public static List<int> FromMask(int mask) => Enumerable.Range(0, 7).Where(d => (mask & (1 << d)) != 0).ToList();

    public static bool IsBlocked(int mask, DateOnly date) => (mask & (1 << (int)date.DayOfWeek)) != 0;

    /// <summary>The blocked dates from <paramref name="start"/> to <paramref name="end"/>, inclusive.</summary>
    public static List<DateOnly> Dates(int mask, DateOnly start, DateOnly end)
    {
        var dates = new List<DateOnly>();
        if (mask == 0) return dates;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (IsBlocked(mask, d)) dates.Add(d);
        return dates;
    }

    /// <summary>E.g. "Sundays" or "Saturdays, Sundays".</summary>
    public static string Describe(int mask) =>
        string.Join(", ", FromMask(mask).Select(d => ((DayOfWeek)d) + "s"));
}

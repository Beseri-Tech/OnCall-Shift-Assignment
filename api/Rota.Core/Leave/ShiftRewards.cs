namespace Rota.Core.Leave;

/// <summary>What an officer's busy-day shifts in one rota earned them for the next one.</summary>
public sealed record ShiftReward(int Holidays, int Weekends, int Peaks, int Points)
{
    public static readonly ShiftReward None = new(0, 0, 0, 0);

    /// <summary>E.g. "1 public holiday, 2 weekend days, 1 peak day".</summary>
    public string Describe() => string.Join(", ", new[]
    {
        Holidays > 0 ? $"{Holidays} public holiday{(Holidays == 1 ? "" : "s")}" : null,
        Weekends > 0 ? $"{Weekends} weekend day{(Weekends == 1 ? "" : "s")}" : null,
        Peaks > 0 ? $"{Peaks} peak day{(Peaks == 1 ? "" : "s")}" : null,
    }.Where(x => x is not null));
}

public static class ShiftRewardPolicy
{
    /// <summary>
    /// Reward for the days an officer worked: a public holiday (also on a weekend) earns the holiday reward, a Saturday or Sunday the
    /// weekend reward, a peak weekday the peak reward; ordinary weekdays earn nothing.
    /// </summary>
    public static ShiftReward For(LeaveLimits limits, IEnumerable<DateOnly> shifts, IReadOnlySet<DateOnly> holidays, IReadOnlySet<DateOnly> peaks)
    {
        int h = 0, w = 0, p = 0;
        foreach (var d in shifts.Distinct())
        {
            if (holidays.Contains(d)) h++;
            else if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) w++;
            else if (peaks.Contains(d)) p++;
        }
        return new ShiftReward(h, w, p, h * limits.HolidayReward + w * limits.WeekendReward + p * limits.PeakReward);
    }
}

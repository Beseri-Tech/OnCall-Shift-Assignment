namespace Rota.Core;

/// <summary>
/// A state's weekly rest days as a bitmask (bit n = <see cref="DayOfWeek"/> n): Saturday + Sunday in most states, Friday +
/// Saturday in Kedah, Kelantan and Terengganu.
/// </summary>
public static class Weekends
{
    public const int SaturdaySunday = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

    public static bool Is(DateOnly d, int mask) => (mask & (1 << (int)d.DayOfWeek)) != 0;
}

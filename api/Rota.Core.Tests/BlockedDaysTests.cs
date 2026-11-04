using Rota.Core.Leave;

namespace Rota.Core.Tests;

public class BlockedDaysTests
{
    [Fact]
    public void Mask_round_trips_and_ignores_junk()
    {
        int mask = BlockedDays.ToMask([0, 6, 6, 9, -1]);
        Assert.Equal([0, 6], BlockedDays.FromMask(mask));
        Assert.Equal(0, BlockedDays.ToMask(null));
        Assert.Equal("Sundays, Saturdays", BlockedDays.Describe(mask));
    }

    [Fact]
    public void Dates_lists_every_blocked_weekday_in_the_range()
    {
        int sundays = BlockedDays.ToMask([(int)DayOfWeek.Sunday]);
        Assert.Equal([new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 25)],
            BlockedDays.Dates(sundays, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 30)));
        Assert.Empty(BlockedDays.Dates(0, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 30)));
        Assert.True(BlockedDays.IsBlocked(sundays, new DateOnly(2026, 10, 4)));
        Assert.False(BlockedDays.IsBlocked(sundays, new DateOnly(2026, 10, 5)));
    }
}

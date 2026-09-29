using Rota.Core.Leave;

namespace Rota.Core.Tests;

public class LeavePolicyTests
{
    // Oct 2026: 1 Oct is a Thursday; 9 weekend days (3-4, 10-11, 17-18, 24-25, 31).
    private static readonly DateOnly Start = new(2026, 10, 1), End = new(2026, 10, 31);
    private static readonly DateOnly Sat = new(2026, 10, 3), Sun = new(2026, 10, 4), Mon = new(2026, 10, 5), Tue = new(2026, 10, 6);

    private static LeavePolicy Policy(DateOnly[]? holidays = null, DateOnly[]? peaks = null) =>
        new(new LeaveLimits(), Start, End, (holidays ?? []).ToHashSet(), (peaks ?? []).ToHashSet());

    [Fact]
    public void Day_kinds_and_costs()
    {
        var p = Policy(holidays: [Sun, Mon], peaks: [Sat, Tue]);
        Assert.Equal((DayKind.Weekend, 1), (p.Kind(Sat), p.Cost(Sat)));   // peak on a weekend is just a weekend
        Assert.Equal((DayKind.Holiday, 2), (p.Kind(Sun), p.Cost(Sun)));   // holiday on a weekend is a holiday
        Assert.Equal((DayKind.Holiday, 2), (p.Kind(Mon), p.Cost(Mon)));
        Assert.Equal((DayKind.Peak, 1), (p.Kind(Tue), p.Cost(Tue)));
        Assert.Equal(0, p.Cost(new DateOnly(2026, 10, 7)));
        Assert.Equal(6, p.Points([Sat, Sun, Mon, Tue, Tue]));
    }

    [Fact]
    public void Budget_allowance_and_cap_scale_with_the_period()
    {
        var p = Policy(holidays: [Mon]);
        Assert.Equal(3, p.DefaultBudget);        // 10 weekend/holiday days * 30%
        Assert.Equal(10, p.WeekdayAllowance);    // 21 ordinary weekdays * 50%, floored
        Assert.Equal(17, p.DayCap(Sat, 58));                   // weekend: 30%
        Assert.Equal(17, p.DayCap(Mon, 58));                   // holiday: 30%
        Assert.Equal(29, p.DayCap(new DateOnly(2026, 10, 7), 58)); // ordinary weekday: 50%
        Assert.Equal(1, p.DayCap(Sat, 2));
    }

    [Fact]
    public void Over_budget_or_allowance_is_refused()
    {
        var p = Policy();
        var errors = p.Validate([], [Sat, Sun, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 11)], new Dictionary<DateOnly, int>(), 3, 10);
        Assert.Contains(errors, e => e.StartsWith("Not enough points: 4 needed, 3 available"));

        var weekdays = Enumerable.Range(5, 16).Select(d => new DateOnly(2026, 10, d)).Where(d => p.Kind(d) == DayKind.Weekday).ToList();
        Assert.Contains(p.Validate([], weekdays, new Dictionary<DateOnly, int>(), 3, 10), e => e.StartsWith("Too many weekdays"));
    }

    [Fact]
    public void Changes_that_do_not_make_things_worse_are_allowed()
    {
        var p = Policy();
        DateOnly[] before = [Sat, Sun, new DateOnly(2026, 10, 10)];
        var full = new Dictionary<DateOnly, int> { [Sat] = 5, [Mon] = 5, [Tue] = 4 };

        // Budget was lowered to 1 after they saved 3 points: removing one day is still fine, keeping full days too.
        Assert.Empty(p.Validate(before, [Sat, Sun], full, 1, 10));
        Assert.Empty(p.Validate(before, before, full, 1, 10));

        // Only newly added days are checked against the cap (10 officers: 3 on weekends, 5 on weekdays).
        Assert.Equal(["Already full (at most 3 of 10 officers off on a weekend/public holiday/peak day, 5 on other days): 3 Oct, 5 Oct."],
            p.Validate([], [Sat, Mon, Tue], full, 5, 10));
    }
}

using Rota.Core.Generation;

namespace Rota.Core.Tests;

public class RotaGeneratorTests
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    private static RotaPerson P(int i, IEnumerable<DateOnly>? leave = null, IEnumerable<DateOnly>? preferred = null,
        bool extra = false, bool preferWeekend = false, int? prior = null) =>
        new(Guid.NewGuid(), $"P{i}", (leave ?? []).ToHashSet(), (preferred ?? []).ToHashSet(),
            extra, preferWeekend, 1, prior);

    private static IEnumerable<DateOnly> Range(DateOnly from, int days) =>
        Enumerable.Range(0, days).Select(from.AddDays);

    private static RotaResult Run(IReadOnlyList<RotaPerson> people, DateOnly start, DateOnly end,
        IEnumerable<DateOnly>? holidays = null) =>
        new RotaGenerator().Generate(new RotaInput(people, start, end, (holidays ?? []).ToHashSet(), Seed: 42));

    private static Dictionary<Guid, List<DateOnly>> ByPerson(RotaResult r) =>
        r.Assignments.Where(a => a.PersonId.HasValue)
            .GroupBy(a => a.PersonId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Date).Order().ToList());

    private static int ConsecutivePairs(RotaResult r) =>
        ByPerson(r).Values.Sum(ds => ds.Zip(ds.Skip(1)).Count(p => p.Second.DayNumber - p.First.DayNumber == 1));

    [Fact]
    public void Every_day_is_covered_once_with_equal_totals_and_no_back_to_back()
    {
        var people = Enumerable.Range(1, 12).Select(i => P(i)).ToList();
        var result = Run(people, Oct1, new DateOnly(2026, 10, 31));

        Assert.Equal(31, result.Assignments.Count);
        Assert.All(result.Assignments, a => Assert.NotNull(a.PersonId));
        Assert.Equal(0, ConsecutivePairs(result));

        var totals = result.Stats.Select(s => s.Total).ToList();
        Assert.True(totals.Max() - totals.Min() <= 1);
    }

    [Fact]
    public void Nobody_is_scheduled_on_leave_and_leave_heavy_people_still_reach_target()
    {
        var people = new List<RotaPerson>
        {
            P(1, leave: Range(Oct1, 15)),
            P(2, leave: Range(new DateOnly(2026, 10, 16), 16)),
            P(3, preferWeekend: true), P(4), P(5),
        };
        var result = Run(people, Oct1, new DateOnly(2026, 10, 31));

        var byPerson = ByPerson(result);
        foreach (var p in people)
            Assert.DoesNotContain(byPerson.GetValueOrDefault(p.Id, []), d => p.Leave.Contains(d));

        Assert.All(result.Assignments, a => Assert.NotNull(a.PersonId));
        Assert.Equal(0, ConsecutivePairs(result));
        Assert.All(result.Stats, s => Assert.Equal(s.TargetTotal, s.Total));
    }

    [Fact]
    public void Holidays_outside_the_range_are_ignored_and_holidays_inside_count_as_weekend()
    {
        var people = Enumerable.Range(1, 4).Select(i => P(i)).ToList();
        var inside = new DateOnly(2026, 10, 20);   // a Tuesday
        var result = Run(people, Oct1, new DateOnly(2026, 10, 31), [inside, new DateOnly(2027, 3, 1)]);

        Assert.Equal(31, result.Assignments.Count);
        Assert.True(result.Assignments.Single(a => a.Date == inside).IsWeekendHoliday);
        Assert.All(result.Assignments, a => Assert.InRange(a.Date, Oct1, new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Preferred_dates_are_honoured_and_clashes_are_reported()
    {
        var xmasEve = new DateOnly(2026, 12, 24);
        var people = Enumerable.Range(1, 8).Select(i => P(i)).ToList();
        people[0] = P(1, preferred: [xmasEve]);
        people[1] = P(2, preferred: [xmasEve]);
        people[2] = P(3, preferred: [new DateOnly(2026, 12, 30)]);

        var result = Run(people, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));
        var owner = result.Assignments.Single(a => a.Date == xmasEve).PersonId;

        Assert.Contains(owner, new Guid?[] { people[0].Id, people[1].Id });
        Assert.Equal(people[2].Id, result.Assignments.Single(a => a.Date == new DateOnly(2026, 12, 30)).PersonId);
        Assert.Contains(result.Warnings, w => w.Contains("also preferred by"));
    }

    [Fact]
    public void Person_on_leave_for_the_whole_rota_gets_zero_target_and_others_absorb_it()
    {
        var people = Enumerable.Range(1, 10).Select(i => P(i)).ToList();
        people[0] = P(1, leave: Range(Oct1, 31));
        var result = Run(people, Oct1, new DateOnly(2026, 10, 31));

        var stat = result.Stats.Single(s => s.PersonId == people[0].Id);
        Assert.Equal(0, stat.TargetTotal);
        Assert.Equal(0, stat.Total);
        Assert.Contains(result.Warnings, w => w.StartsWith("P1 can work at most 0 shifts"));
        Assert.All(result.Assignments, a => Assert.NotNull(a.PersonId));
        Assert.All(result.Stats.Where(s => s.PersonId != people[0].Id), s => Assert.InRange(s.Total, 3, 4));
    }

    [Fact]
    public void People_with_fewer_past_weekend_shifts_get_the_weekends()
    {
        var people = Enumerable.Range(1, 12).Select(i => P(i, prior: i <= 4 ? 0 : 6)).ToList();
        var result = Run(people, Oct1, new DateOnly(2026, 10, 31));

        var low = result.Stats.Where(s => people.Take(4).Any(p => p.Id == s.PersonId)).Sum(s => s.WeekendHoliday);
        var high = result.Stats.Where(s => people.Skip(4).Any(p => p.Id == s.PersonId)).Sum(s => s.WeekendHoliday);

        Assert.True(low > high, $"low-history people got {low} weekend shifts vs {high}");
    }

    [Fact]
    public void Two_people_alternate_without_consecutive_days()
    {
        var people = new List<RotaPerson> { P(1), P(2) };
        var result = Run(people, Oct1, new DateOnly(2026, 10, 14));

        Assert.Equal(0, ConsecutivePairs(result));
        Assert.All(result.Stats, s => Assert.Equal(7, s.Total));
    }

    [Fact]
    public void Same_seed_gives_the_same_rota()
    {
        var people = Enumerable.Range(1, 8).Select(i => P(i)).ToList();
        var a = Run(people, Oct1, new DateOnly(2026, 11, 30));
        var b = Run(people, Oct1, new DateOnly(2026, 11, 30));

        Assert.Equal(a.Assignments, b.Assignments);
    }

    [Fact]
    public void Day_with_everyone_on_leave_is_left_unassigned_and_reported()
    {
        var gap = new DateOnly(2026, 10, 10);
        var people = Enumerable.Range(1, 3).Select(i => P(i, leave: [gap])).ToList();
        var result = Run(people, Oct1, new DateOnly(2026, 10, 15));

        Assert.Null(result.Assignments.Single(a => a.Date == gap).PersonId);
        Assert.Contains(result.Warnings, w => w.Contains("could not be assigned"));
    }
}

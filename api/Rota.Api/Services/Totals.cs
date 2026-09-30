using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Services;

/// <summary>Shifts done so far = the admin's tally adjustment + assignments in published runs.</summary>
public sealed record PersonTotals(int Total, int Weekday, int WeekendHoliday, bool Known, int PublishedWeekday = 0,
    int PublishedWeekendHoliday = 0)
{
    public static readonly PersonTotals Unknown = new(0, 0, 0, false);
}

public static class Totals
{
    /// <summary>
    /// Computed on read, so publishing/unpublishing a run updates everyone's totals without
    /// any stored counter that could drift or double count.
    /// </summary>
    /// <param name="excludePeriodId">Leave out this period's published run (when regenerating that period).</param>
    public static async Task<Dictionary<Guid, PersonTotals>> ForPeopleAsync(RotaDbContext db, Guid? excludePeriodId = null,
        CancellationToken ct = default)
    {
        var published = await db.Assignments
            .Where(a => a.PersonId != null &&
                        db.Runs.Any(r => r.Id == a.RunId && r.IsPublished &&
                                          (excludePeriodId == null || r.PeriodId != excludePeriodId.Value)))
            .GroupBy(a => a.PersonId!.Value)
            .Select(g => new
            {
                PersonId = g.Key,
                Total = g.Count(),
                WeekendHoliday = g.Count(a => a.IsWeekendHoliday),
            })
            .ToDictionaryAsync(x => x.PersonId, ct);

        var people = await db.People
            .Select(p => new { p.Id, p.TallyWeekdayAdjust, p.TallyWeekendHolidayAdjust })
            .ToListAsync(ct);

        return people.ToDictionary(p => p.Id, p =>
        {
            published.TryGetValue(p.Id, out var pub);
            int pubWeekend = pub?.WeekendHoliday ?? 0;
            int pubWeekday = (pub?.Total ?? 0) - pubWeekend;

            bool known = p.TallyWeekdayAdjust.HasValue || p.TallyWeekendHolidayAdjust.HasValue || pub is not null;
            int weekday = (p.TallyWeekdayAdjust ?? 0) + pubWeekday;
            int weekend = (p.TallyWeekendHolidayAdjust ?? 0) + pubWeekend;
            return new PersonTotals(weekday + weekend, weekday, weekend, known, pubWeekday, pubWeekend);
        });
    }
}

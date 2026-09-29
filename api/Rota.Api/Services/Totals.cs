using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Services;

/// <summary>Shifts done so far = opening numbers + assignments in published runs.</summary>
public sealed record PersonTotals(int Total, int Weekday, int WeekendHoliday, bool Known)
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
            .Select(p => new { p.Id, p.OpeningTotal, p.OpeningWeekday, p.OpeningWeekendHoliday })
            .ToListAsync(ct);

        return people.ToDictionary(p => p.Id, p =>
        {
            published.TryGetValue(p.Id, out var pub);
            int pubTotal = pub?.Total ?? 0;
            int pubWeekend = pub?.WeekendHoliday ?? 0;

            bool known = p.OpeningTotal.HasValue || p.OpeningWeekday.HasValue || p.OpeningWeekendHoliday.HasValue || pubTotal > 0;
            int openingWeekend = p.OpeningWeekendHoliday ?? 0;
            int openingWeekday = p.OpeningWeekday ?? Math.Max(0, (p.OpeningTotal ?? 0) - openingWeekend);
            int openingTotal = p.OpeningTotal ?? openingWeekday + openingWeekend;

            return new PersonTotals(
                openingTotal + pubTotal,
                openingWeekday + (pubTotal - pubWeekend),
                openingWeekend + pubWeekend,
                known);
        });
    }
}

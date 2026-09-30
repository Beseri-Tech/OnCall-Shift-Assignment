using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Excel;

namespace Rota.Api.Endpoints;

/// <summary>The admin's view of shifts done per officer; edits are stored as adjustments on top of published rotas.</summary>
public static class AdminTallyEndpoints
{
    public static void MapTallyEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/tally").WithTags("Admin tally");

        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) => await RowsAsync(db, ct));

        g.MapPut("/", async (List<TallyUpdate> updates, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (updates.Any(u => u.Weekday is < 0 or > 10_000 || u.WeekendHoliday is < 0 or > 10_000))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["tally"] = ["Numbers must be 0 or more."] });

            var ids = updates.Select(u => u.PersonId).ToHashSet();
            var people = await db.People.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            if (people.Count != ids.Count) return Results.NotFound();

            var totals = await Totals.ForPeopleAsync(db, ct: ct);
            foreach (var u in updates)
            {
                var t = totals[u.PersonId];
                var p = people[u.PersonId];
                // Store the difference so "adjustment + published" shows exactly what the admin typed.
                p.TallyWeekdayAdjust = u.Weekday - t.PublishedWeekday;
                p.TallyWeekendHolidayAdjust = u.WeekendHoliday - t.PublishedWeekendHoliday;
                Mapping.Log(db, http, "tally.update", p.Id, new
                {
                    before = new { t.Weekday, t.WeekendHoliday },
                    after = new { u.Weekday, u.WeekendHoliday },
                });
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(await RowsAsync(db, ct));
        });

        g.MapGet("/export.xlsx", async (RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var rows = (await RowsAsync(db, ct)).Where(r => r.Status == OfficerStatus.OnCall)
                .Select(r => new PersonTotalsRow(r.Code, r.Name, r.Total, r.Weekday, r.WeekendHoliday)).ToList();
            return Results.File(ExcelExports.Totals(rows, clock.Today),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Shift tally {clock.Today:yyyy-MM-dd}.xlsx");
        });
    }

    private static async Task<List<TallyRowDto>> RowsAsync(RotaDbContext db, CancellationToken ct)
    {
        var totals = await Totals.ForPeopleAsync(db, ct: ct);
        var people = await db.People.Include(p => p.Clinic).OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        return people.Select(p =>
        {
            var t = totals.GetValueOrDefault(p.Id, PersonTotals.Unknown);
            return new TallyRowDto(p.Id, p.Code, p.Name, p.Status, p.Clinic?.Name, p.Clinic?.Area,
                t.Weekday, t.WeekendHoliday, t.Total, t.PublishedWeekday, t.PublishedWeekendHoliday, t.Known);
        }).ToList();
    }
}

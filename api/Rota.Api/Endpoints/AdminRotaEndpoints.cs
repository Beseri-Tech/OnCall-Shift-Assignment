using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Excel;
using Rota.Core.Generation;
using Rota.Core.Leave;

namespace Rota.Api.Endpoints;

public static class AdminRotaEndpoints
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapRotaEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.WithTags("Admin rota");

        g.MapPost("/periods/{id:guid}/generate", async (Guid id, GenerateRequest? req, RotaDbContext db, HttpContext http,
            ILogger<RotaGenerator> log, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();
            if (period.Status == PeriodStatus.Open)
                return Results.Problem("Lock the period first so leave can't change while you generate.",
                    statusCode: StatusCodes.Status409Conflict);

            var input = await BuildInputAsync(db, period, req?.Seed, ct);
            if (input.People.Count == 0)
                return Results.Problem("There are no active people.", statusCode: StatusCodes.Status409Conflict);

            var result = await Task.Run(() => new RotaGenerator().Generate(input, cancellationToken: ct), ct);
            log.LogInformation("Generated rota for {Period}: seed {Seed}, objective {Objective:F1}", period.Name, result.Seed, result.Objective);

            var run = new RotaRun
            {
                PeriodId = id,
                Seed = result.Seed,
                Objective = result.Objective,
                Warnings = result.Warnings.ToList(),
                Assignments = result.Assignments.Select(a => new Data.ShiftAssignment
                {
                    Date = a.Date, PersonId = a.PersonId, IsWeekendHoliday = a.IsWeekendHoliday,
                }).ToList(),
            };
            db.Runs.Add(run);
            Mapping.Log(db, http, "run.generate", null, new { period = period.Name, run.Seed });
            await db.SaveChangesAsync(ct);

            return Results.Ok(await RunDetailAsync(db, run.Id, ct));
        });

        g.MapGet("/periods/{id:guid}/runs", async (Guid id, RotaDbContext db, CancellationToken ct) =>
        {
            var runs = await db.Runs.Include(r => r.Assignments).Where(r => r.PeriodId == id)
                .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
            return runs.Select(ToDto);
        });

        g.MapGet("/runs/{id:guid}", async (Guid id, RotaDbContext db, CancellationToken ct) =>
            await RunDetailAsync(db, id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());

        g.MapPut("/runs/{id:guid}/assignments/{date}", async (Guid id, DateOnly date, OverrideRequest req, RotaDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            var run = await db.Runs.Include(r => r.Assignments).FirstOrDefaultAsync(r => r.Id == id, ct);
            var slot = run?.Assignments.FirstOrDefault(a => a.Date == date);
            if (run is null || slot is null) return Results.NotFound();
            if (run.IsPublished)
                return Results.Problem("Unpublish the rota before changing it.", statusCode: StatusCodes.Status409Conflict);

            var warnings = new List<string>();
            if (req.PersonId is { } pid)
            {
                var person = await db.People.FindAsync([pid], ct);
                if (person is null) return Results.NotFound();
                if (person.Status != OfficerStatus.OnCall) warnings.Add($"{person.Name} is not on call ({person.StatusReason ?? person.Status.ToString()}).");
                if (await db.LeaveDays.AnyAsync(l => l.PersonId == pid && l.Date == date, ct))
                    warnings.Add($"{person.Name} is on leave on {LeaveText.Format(date)}.");

                var neighbours = run.Assignments.Where(a => a.PersonId == pid && Math.Abs(a.Date.DayNumber - date.DayNumber) == 1);
                warnings.AddRange(neighbours.Select(a => $"{person.Name} also works {LeaveText.Format(a.Date)} (back-to-back)."));
            }

            slot.PersonId = req.PersonId;
            slot.IsManual = true;
            Mapping.Log(db, http, "run.override", req.PersonId, new { run = id, date });
            await db.SaveChangesAsync(ct);

            var detail = await RunDetailAsync(db, id, ct);
            return Results.Ok(new OverrideResponse(detail!.Days.Single(d => d.Date == date), warnings));
        });

        g.MapPost("/runs/{id:guid}/publish", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var run = await db.Runs.Include(r => r.Period).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run?.Period is null) return Results.NotFound();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Runs.Where(r => r.PeriodId == run.PeriodId && r.IsPublished && r.Id != id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsPublished, false).SetProperty(r => r.PublishedAt, (DateTimeOffset?)null), ct);

            run.IsPublished = true;
            run.PublishedAt = DateTimeOffset.UtcNow;
            run.Period.Status = PeriodStatus.Published;
            Mapping.Log(db, http, "run.publish", null, new { period = run.Period.Name, run = id });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/runs/{id:guid}/unpublish", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var run = await db.Runs.Include(r => r.Period).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run?.Period is null) return Results.NotFound();
            if (!run.IsPublished) return Results.NoContent();

            run.IsPublished = false;
            run.PublishedAt = null;
            run.Period.Status = PeriodStatus.Locked;
            Mapping.Log(db, http, "run.unpublish", null, new { period = run.Period.Name, run = id });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/runs/{id:guid}", async (Guid id, RotaDbContext db, CancellationToken ct) =>
        {
            var run = await db.Runs.FindAsync([id], ct);
            if (run is null) return Results.NotFound();
            if (run.IsPublished)
                return Results.Problem("Unpublish the rota before deleting it.", statusCode: StatusCodes.Status409Conflict);

            db.Runs.Remove(run);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ---------------- exports ----------------

        g.MapGet("/runs/{id:guid}/timetable.xlsx", async (Guid id, RotaDbContext db, CancellationToken ct) =>
        {
            var run = await db.Runs.Include(r => r.Period).Include(r => r.Assignments).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run?.Period is null) return Results.NotFound();

            var names = await db.People.ToDictionaryAsync(p => p.Id, p => p.Name, ct);
            var holidays = await Mapping.HolidaysAsync(db, run.Period.StartDate, run.Period.EndDate, ct);
            var days = run.Assignments.OrderBy(a => a.Date)
                .Select(a => new TimetableDay(a.Date, a.PersonId is { } p ? names.GetValueOrDefault(p) : null,
                    a.IsWeekendHoliday, holidays.ContainsKey(a.Date)))
                .ToList();

            return Results.File(ExcelExports.Timetable(days), Xlsx, $"Rota {run.Period.Name}.xlsx");
        });

        g.MapGet("/people/totals.xlsx", async (RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var people = await AdminEndpoints.AdminPeopleAsync(db, ct);
            var rows = people.Where(p => p.Status == OfficerStatus.OnCall)
                .Select(p => new PersonTotalsRow(p.Code, p.Name, p.Totals.Total, p.Totals.Weekday, p.Totals.WeekendHoliday))
                .ToList();
            return Results.File(ExcelExports.Totals(rows, clock.Today), Xlsx, $"Shift totals {clock.Today:yyyy-MM-dd}.xlsx");
        });

        g.MapGet("/periods/{id:guid}/leave.xlsx", async (Guid id, RotaDbContext db, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();

            var rows = await db.People.Where(p => p.Status == OfficerStatus.OnCall).OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .Select(p => new LeaveExportRow(p.Name,
                    p.Leave.Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate).Select(l => l.Date).ToList()))
                .ToListAsync(ct);
            return Results.File(ExcelExports.LeaveSheet(rows, period.StartDate, period.EndDate), Xlsx, $"Leave {period.Name}.xlsx");
        });
    }

    internal static async Task<RotaInput> BuildInputAsync(RotaDbContext db, RotaPeriod period, int? seed, CancellationToken ct)
    {
        var totals = await Totals.ForPeopleAsync(db, excludePeriodId: period.Id, ct: ct);
        var holidays = await db.Holidays.Where(h => h.Date >= period.StartDate && h.Date <= period.EndDate)
            .Select(h => h.Date).ToListAsync(ct);

        var people = await db.People.Where(p => p.Status == OfficerStatus.OnCall).OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.ExtraShift, p.PreferWeekendHoliday, p.WeekendWeight,
                Leave = p.Leave.Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate).Select(l => l.Date).ToList(),
                Preferred = p.PreferredDates.Where(d => d.Date >= period.StartDate && d.Date <= period.EndDate).Select(d => d.Date).ToList(),
            })
            .ToListAsync(ct);

        var rotaPeople = people.Select(p =>
        {
            var t = totals.GetValueOrDefault(p.Id, PersonTotals.Unknown);
            return new RotaPerson(p.Id, p.Name, p.Leave.ToHashSet(), p.Preferred.ToHashSet(), p.ExtraShift,
                p.PreferWeekendHoliday, p.WeekendWeight, t.Known ? t.WeekendHoliday : null);
        }).ToList();

        return new RotaInput(rotaPeople, period.StartDate, period.EndDate, holidays.ToHashSet(), seed);
    }

    private static RunDto ToDto(RotaRun r)
    {
        var assigned = r.Assignments.Where(a => a.PersonId != null).OrderBy(a => a.Date).ToList();
        int consecutive = assigned.GroupBy(a => a.PersonId)
            .Sum(g => g.Zip(g.Skip(1)).Count(p => p.Second.Date.DayNumber - p.First.Date.DayNumber == 1));

        return new RunDto(r.Id, r.PeriodId, r.CreatedAt, r.Seed, r.IsPublished, r.Warnings,
            r.Assignments.Count(a => a.PersonId == null), consecutive, r.Assignments.Count(a => a.IsManual));
    }

    internal static async Task<RunDetailDto?> RunDetailAsync(RotaDbContext db, Guid id, CancellationToken ct)
    {
        var run = await db.Runs.Include(r => r.Period).Include(r => r.Assignments).AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run?.Period is null) return null;

        var period = run.Period;
        var people = await db.People.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        var names = people.ToDictionary(p => p.Id, p => p.Name);
        var holidays = await Mapping.HolidaysAsync(db, period.StartDate, period.EndDate, ct);
        var totals = await Totals.ForPeopleAsync(db, excludePeriodId: period.Id, ct: ct);
        var leaveCounts = await db.LeaveDays.Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate)
            .GroupBy(l => l.PersonId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var byPerson = run.Assignments.Where(a => a.PersonId != null).GroupBy(a => a.PersonId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var stats = people.Where(p => p.Status == OfficerStatus.OnCall || byPerson.ContainsKey(p.Id))
            .Select(p =>
            {
                var mine = byPerson.GetValueOrDefault(p.Id, []);
                var t = totals.GetValueOrDefault(p.Id, PersonTotals.Unknown);
                return new RunStatDto(p.Id, p.Code, p.Name, mine.Count, mine.Count(a => !a.IsWeekendHoliday),
                    mine.Count(a => a.IsWeekendHoliday), leaveCounts.GetValueOrDefault(p.Id), t.Known ? t.WeekendHoliday : null);
            })
            .ToList();

        return new RunDetailDto(ToDto(run), Mapping.ToDays(run.Assignments, names, holidays), stats);
    }
}

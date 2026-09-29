using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Leave;

namespace Rota.Api.Endpoints;

/// <summary>No login yet: people pick their name. Edits are logged in change_log.</summary>
public static class PublicEndpoints
{
    private const int MaxNoteLength = 200;

    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Public");

        api.MapGet("/people", async (RotaDbContext db, CancellationToken ct) =>
            await db.People.Where(p => p.Status == OfficerStatus.OnCall)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .Select(p => new PersonSummaryDto(p.Id, p.Code, p.Name))
                .ToListAsync(ct));

        api.MapGet("/people/{id:guid}", async (Guid id, RotaDbContext db, CancellationToken ct) =>
        {
            var p = await db.People.FindAsync([id], ct);
            if (p is null) return Results.NotFound();

            var totals = (await Totals.ForPeopleAsync(db, ct: ct)).GetValueOrDefault(id, PersonTotals.Unknown);
            return Results.Ok(new PersonProfileDto(p.Id, p.Code, p.Name, totals.ToDto(), totals.Known));
        });

        api.MapGet("/periods", async (PeriodStatus? status, RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var q = db.Periods.AsQueryable();
            if (status is { } s) q = q.Where(p => p.Status == s);
            var periods = await q.OrderBy(p => p.StartDate).ToListAsync(ct);
            return periods.Select(p => p.ToDto(clock.Today));
        });

        api.MapGet("/holidays", async (DateOnly? from, DateOnly? to, RotaDbContext db, CancellationToken ct) =>
        {
            var q = db.Holidays.AsQueryable();
            if (from is { } f) q = q.Where(h => h.Date >= f);
            if (to is { } t) q = q.Where(h => h.Date <= t);
            return await q.OrderBy(h => h.Date).Select(h => new HolidayDto(h.Date, h.Name)).ToListAsync(ct);
        });

        api.MapGet("/people/{id:guid}/entries", async (Guid id, Guid periodId, RotaDbContext db, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([periodId], ct);
            if (period is null || !await db.People.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();

            return Results.Ok(await LoadEntriesAsync(db, id, period, ct));
        });

        api.MapGet("/periods/{id:guid}/leave-rules", async (Guid id, Guid personId, RotaDbContext db, LeaveLimits limits,
            CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();

            var rules = await LeaveRules.LoadAsync(db, limits, period, ct);
            var grant = rules.Grants.GetValueOrDefault(personId);
            return Results.Ok(new LeaveRulesDto(
                rules.BaseBudget, grant?.Points ?? 0, grant?.Reason, rules.Policy.WeekdayAllowance,
                rules.Policy.BusyDayCap(rules.OnCall), rules.Policy.WeekdayCap(rules.OnCall), rules.OnCall, limits.WeekendCost, limits.HolidayCost, limits.PeakCost,
                rules.Peaks.Select(p => new HolidayDto(p.Date, p.Name)).ToList(), rules.OthersOff(personId)));
        });

        api.MapPut("/people/{id:guid}/entries", async (Guid id, Guid periodId, EntriesDto body, RotaDbContext db,
            LocalClock clock, LeaveLimits limits, HttpContext http, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([periodId], ct);
            var person = await db.People.FindAsync([id], ct);
            if (period is null || person is null || person.Status != OfficerStatus.OnCall) return Results.NotFound();

            if (!period.IsEditable(clock.Today))
                return Results.Problem(
                    period.Status == PeriodStatus.Open ? "The leave deadline for this period has passed." : "This period is locked.",
                    statusCode: StatusCodes.Status409Conflict);

            var outside = body.Leave.Select(l => l.Date).Concat(body.Preferred)
                .Where(d => d < period.StartDate || d > period.EndDate).Distinct().Order().ToList();
            if (outside.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["dates"] = [$"Dates outside {period.Name}: {LeaveText.Compact(outside)}"],
                });

            if (body.Leave.Any(l => l.Note?.Length > MaxNoteLength))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["note"] = [$"Notes can be at most {MaxNoteLength} characters."],
                });

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // One save per period at a time (released on commit/rollback), so two officers can't both take the last spot on a day.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({period.Id.ToString()}, 0))", ct);

            // Read after the lock so we see what the previous save just committed.
            var before = await LoadEntriesAsync(db, id, period, ct);
            var rules = await LeaveRules.LoadAsync(db, limits, period, ct);
            var limitErrors = rules.Validate(id, before.Leave.Select(l => l.Date).ToList(), body.Leave.Select(l => l.Date).Distinct().ToList());
            if (limitErrors.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["leave"] = [.. limitErrors] });

            await db.LeaveDays.Where(l => l.PersonId == id && l.Date >= period.StartDate && l.Date <= period.EndDate)
                .ExecuteDeleteAsync(ct);
            await db.PreferredDates.Where(p => p.PersonId == id && p.Date >= period.StartDate && p.Date <= period.EndDate)
                .ExecuteDeleteAsync(ct);

            var leave = body.Leave.GroupBy(l => l.Date)
                .Select(g => new LeaveDay { PersonId = id, Date = g.Key, Note = Clean(g.Last().Note) })
                .ToList();
            var leaveDates = leave.Select(l => l.Date).ToHashSet();
            var preferred = body.Preferred.Distinct()
                .Where(d => !leaveDates.Contains(d))   // can't prefer a day you're on leave
                .Select(d => new PreferredDate { PersonId = id, Date = d })
                .ToList();

            db.LeaveDays.AddRange(leave);
            db.PreferredDates.AddRange(preferred);
            Mapping.Log(db, http, "entries.update", id, new
            {
                period = period.Name,
                leaveBefore = LeaveText.Compact(before.Leave.Select(l => l.Date)),
                leaveAfter = LeaveText.Compact(leave.Select(l => l.Date)),
                preferredAfter = LeaveText.Compact(preferred.Select(p => p.Date)),
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Ok(await LoadEntriesAsync(db, id, period, ct));
        });

        api.MapPost("/leave/parse", (ParseRequest req) =>
            LeaveText.TryParseDateList(req.Text, req.Reference, out var dates, out var error)
                ? new ParseResponse(dates, null)
                : new ParseResponse([], error));

        api.MapGet("/periods/{id:guid}/overview", async (Guid id, RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();

            var holidays = await Mapping.HolidaysAsync(db, period.StartDate, period.EndDate, ct);
            var people = await db.People.Where(p => p.Status == OfficerStatus.OnCall)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .Select(p => new
                {
                    p.Id, p.Code, p.Name,
                    Leave = p.Leave.Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate)
                        .OrderBy(l => l.Date).Select(l => new LeaveEntryDto(l.Date, l.Note)).ToList(),
                    Preferred = p.PreferredDates.Where(d => d.Date >= period.StartDate && d.Date <= period.EndDate)
                        .OrderBy(d => d.Date).Select(d => d.Date).ToList(),
                })
                .ToListAsync(ct);

            var onLeave = people.SelectMany(p => p.Leave.Select(l => l.Date)).GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
            var days = Mapping.Days(period.StartDate, period.EndDate)
                .Select(d => new OverviewDay(d, Mapping.IsWeekend(d) || holidays.ContainsKey(d), holidays.GetValueOrDefault(d),
                    people.Count - onLeave.GetValueOrDefault(d)))
                .ToList();

            return Results.Ok(new OverviewDto(
                period.ToDto(clock.Today), days,
                people.Select(p => new OverviewPerson(p.Id, p.Code, p.Name, p.Leave, p.Preferred)).ToList()));
        });

        api.MapGet("/periods/{id:guid}/rota", async (Guid id, RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            var run = await db.Runs.Include(r => r.Assignments).FirstOrDefaultAsync(r => r.PeriodId == id && r.IsPublished, ct);
            if (period is null || run is null) return Results.NotFound();

            var names = await db.People.ToDictionaryAsync(p => p.Id, p => p.Name, ct);
            var holidays = await Mapping.HolidaysAsync(db, period.StartDate, period.EndDate, ct);
            return Results.Ok(new PublishedRotaDto(period.ToDto(clock.Today), run.Id, Mapping.ToDays(run.Assignments, names, holidays)));
        });
    }

    private static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static async Task<EntriesDto> LoadEntriesAsync(RotaDbContext db, Guid personId, RotaPeriod period, CancellationToken ct)
    {
        var leave = await db.LeaveDays
            .Where(l => l.PersonId == personId && l.Date >= period.StartDate && l.Date <= period.EndDate)
            .OrderBy(l => l.Date).Select(l => new LeaveEntryDto(l.Date, l.Note)).ToListAsync(ct);
        var preferred = await db.PreferredDates
            .Where(p => p.PersonId == personId && p.Date >= period.StartDate && p.Date <= period.EndDate)
            .OrderBy(p => p.Date).Select(p => p.Date).ToListAsync(ct);
        return new EntriesDto(leave, preferred);
    }
}

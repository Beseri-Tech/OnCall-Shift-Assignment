using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Leave;

namespace Rota.Api.Endpoints;

/// <summary>Signed-in pages. Officers see and edit their own leave; admins any officer's. Edits are logged in change_log.</summary>
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

        api.MapGet("/people/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!CanAccess(http, id)) return Results.Forbid();
            var p = await db.People.FindAsync([id], ct);
            if (p is null) return Results.NotFound();

            var totals = (await Totals.ForPeopleAsync(db, ct: ct)).GetValueOrDefault(id, PersonTotals.Unknown);
            var clinicDays = p.ClinicId is { } cid
                ? await db.ClinicBlockedDays.Where(d => d.ClinicId == cid).OrderBy(d => d.Date)
                    .Select(d => new ClinicBlockedDayDto(d.ClinicId, db.Clinics.Where(c => c.Id == d.ClinicId).Select(c => c.Name).First(), d.Date, d.Reason))
                    .ToListAsync(ct)
                : [];
            return Results.Ok(new PersonProfileDto(p.Id, p.Code, p.Name, totals.ToDto(), totals.Known, BlockedDays.FromMask(p.BlockedWeekdays), p.BlockedReason,
                clinicDays));
        });

        // Shifts in published rotas, plus rotas in review (flagged: they can still change).
        api.MapGet("/people/{id:guid}/shifts", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!CanAccess(http, id)) return Results.Forbid();

            var shifts = await (
                from a in db.Assignments
                join r in db.Runs on a.RunId equals r.Id
                join p in db.Periods on r.PeriodId equals p.Id
                where a.PersonId == id && (r.IsPublished || r.IsInReview)
                orderby a.Date
                select new { a.Date, a.IsWeekendHoliday, PeriodId = p.Id, PeriodName = p.Name, r.IsInReview, RunId = r.Id })
                .ToListAsync(ct);

            var periodIds = shifts.Select(s => s.PeriodId).Distinct().ToList();
            var holidays = await db.PeriodDays.Where(d => periodIds.Contains(d.PeriodId) && d.Kind == PeriodDayKind.Holiday)
                .ToDictionaryAsync(d => (d.PeriodId, d.Date), d => d.Name, ct);
            return Results.Ok(shifts.Select(s =>
                new ShiftDto(s.Date, s.IsWeekendHoliday, holidays.GetValueOrDefault((s.PeriodId, s.Date)), s.PeriodId, s.PeriodName,
                    s.IsInReview, s.RunId))
                .ToList());
        });

        api.MapGet("/periods", async (PeriodStatus? status, RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var q = db.Periods.AsQueryable();
            if (status is { } s) q = q.Where(p => p.Status == s);
            var periods = await q.OrderBy(p => p.StartDate).ToListAsync(ct);
            return periods.Select(p => p.ToDto(clock.Today));
        });

        // Holidays in effect: each period's own list, and the master list for dates outside every period.
        api.MapGet("/holidays", async (DateOnly? from, DateOnly? to, RotaDbContext db, CancellationToken ct) =>
        {
            DateOnly f = from ?? DateOnly.MinValue, t = to ?? DateOnly.MaxValue;
            var applied = await db.PeriodDays.Where(d => d.Kind == PeriodDayKind.Holiday && d.Date >= f && d.Date <= t)
                .Select(d => new HolidayDto(d.Date, d.Name)).ToListAsync(ct);
            var master = await db.Holidays
                .Where(h => h.Date >= f && h.Date <= t && !db.Periods.Any(p => p.StartDate <= h.Date && h.Date <= p.EndDate))
                .Select(h => new HolidayDto(h.Date, h.Name)).ToListAsync(ct);
            return applied.Concat(master).OrderBy(h => h.Date).ToList();
        });

        api.MapGet("/people/{id:guid}/entries", async (Guid id, Guid periodId, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!CanAccess(http, id)) return Results.Forbid();
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
                rules.Peaks.Select(p => new HolidayDto(p.Date, p.Name)).ToList(), rules.OthersOff(personId),
                rules.ConfirmedDays(personId), rules.PeriodDays, rules.PointsDeducted(personId), rules.WeekdaysDeducted(personId),
                rules.Reward(personId).Points, rules.RewardFrom, rules.Reward(personId).Points > 0 ? rules.Reward(personId).Describe() : null,
                rules.DayCaps()));
        });

        api.MapPut("/people/{id:guid}/entries", async (Guid id, Guid periodId, EntriesDto body, RotaDbContext db,
            LocalClock clock, LeaveLimits limits, HttpContext http, CancellationToken ct) =>
        {
            if (!CanAccess(http, id)) return Results.Forbid();
            // Admins edit on someone's behalf: no deadline and no leave limits (like the old spreadsheet).
            bool byAdmin = http.User.IsAdmin();

            var period = await db.Periods.FindAsync([periodId], ct);
            var person = await db.People.FindAsync([id], ct);
            if (period is null || person is null || person.Status != OfficerStatus.OnCall) return Results.NotFound();
            if (!byAdmin && await db.PeriodExclusions.AnyAsync(x => x.PeriodId == periodId && x.PersonId == id, ct))
                return Results.Problem("You are not included in the rota for this period, so there is no leave to enter. Contact the admin if this is unexpected.",
                    statusCode: StatusCodes.Status409Conflict);

            var outside = body.Leave.Select(l => l.Date).Concat(body.Preferred)
                .Where(d => d < period.StartDate || d > period.EndDate).Distinct().Order().ToList();
            if (outside.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["dates"] = [$"Dates outside {period.Name}: {LeaveText.Compact(outside)}"],
                });

            // Days the admin blocked for this officer (e.g. every Sunday): nothing to ask for there, no points involved.
            // Leave they already had on such days is left alone.
            var blocked = BlockedDays.Dates(person.BlockedWeekdays, period.StartDate, period.EndDate).ToHashSet();
            if (blocked.Count > 0)
            {
                var existing = (await LoadEntriesAsync(db, id, period, ct)).Leave.Select(l => l.Date).ToHashSet();
                var clash = body.Leave.Select(l => l.Date).Where(d => !existing.Contains(d)).Concat(body.Preferred)
                    .Where(blocked.Contains).Distinct().Order().ToList();
                if (clash.Count > 0)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["dates"] = [$"You can't be on call on {BlockedDays.Describe(person.BlockedWeekdays)}, so there is nothing to mark on {LeaveText.Compact(clash)}."],
                    });
            }

            // Days the admin blocked for the officer's whole clinic (e.g. a clinic event): same, nothing to mark.
            var clinicBlocked = (await ClinicBlocks.ByPersonAsync(db, period.StartDate, period.EndDate, ct)).GetValueOrDefault(id) ?? [];
            if (clinicBlocked.Count > 0)
            {
                var existing = (await LoadEntriesAsync(db, id, period, ct)).Leave.Select(l => l.Date).ToHashSet();
                var clash = body.Leave.Select(l => l.Date).Where(d => !existing.Contains(d)).Concat(body.Preferred)
                    .Where(clinicBlocked.Contains).Distinct().Order().ToList();
                if (clash.Count > 0)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["dates"] = [$"Your clinic can't be on call on {LeaveText.Compact(clash)}, so there is nothing to mark there."],
                    });
            }

            if (body.Leave.Any(l => l.Note?.Length > MaxNoteLength))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["note"] = [$"Notes can be at most {MaxNoteLength} characters."],
                });

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // One save per period at a time (released on commit/rollback), so two officers can't both take the last spot on a day.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({period.Id.ToString()}, 0))", ct);

            // Check the period only now, under the lock: locking the period takes the same lock, so a save can't slip through
            // while the admin is locking it.
            await db.Entry(period).ReloadAsync(ct);
            if (!byAdmin && !period.IsEditable(clock.Today))
                return Results.Problem(
                    period.Status == PeriodStatus.Open ? "The leave deadline for this period has passed." : "This period was locked, so leave can no longer be changed. Ask the admin.",
                    statusCode: StatusCodes.Status409Conflict);

            // Read after the lock so we see what the previous save just committed.
            var before = await LoadEntriesAsync(db, id, period, ct);
            var rules = await LeaveRules.LoadAsync(db, limits, period, ct);

            // Confirmed leave is the admin's (Confirmed leave page): it stays as it is, whatever the body says about those days.
            var confirmed = await db.LeaveDays
                .Where(l => l.PersonId == id && l.ApprovalId != null && l.Date >= period.StartDate && l.Date <= period.EndDate)
                .Select(l => l.Date).ToHashSetAsync(ct);
            var ownLeave = body.Leave.Where(l => !confirmed.Contains(l.Date)).ToList();

            var limitErrors = byAdmin ? [] : rules.Validate(id, before.Leave.Select(l => l.Date).ToList(), ownLeave.Select(l => l.Date).Distinct().ToList());
            if (limitErrors.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["leave"] = [.. limitErrors] });

            await db.LeaveDays.Where(l => l.PersonId == id && l.ApprovalId == null && l.Date >= period.StartDate && l.Date <= period.EndDate)
                .ExecuteDeleteAsync(ct);
            await db.PreferredDates.Where(p => p.PersonId == id && p.Date >= period.StartDate && p.Date <= period.EndDate)
                .ExecuteDeleteAsync(ct);

            var leave = ownLeave.GroupBy(l => l.Date)
                .Select(g => new LeaveDay { PersonId = id, Date = g.Key, Note = Clean(g.Last().Note) })
                .ToList();
            var leaveDates = leave.Select(l => l.Date).Concat(confirmed).ToHashSet();
            var preferred = body.Preferred.Distinct()
                .Where(d => !leaveDates.Contains(d))   // can't prefer a day you're on leave
                .Select(d => new PreferredDate { PersonId = id, Date = d })
                .ToList();

            db.LeaveDays.AddRange(leave);
            db.PreferredDates.AddRange(preferred);
            Mapping.Log(db, http, "entries.update", id, new { period = period.Name, byAdmin }, id,
                new { Leave = LeaveText.Compact(before.Leave.Select(l => l.Date)), Preferred = LeaveText.Compact(before.Preferred) },
                new { Leave = LeaveText.Compact(leave.Select(l => l.Date)), Preferred = LeaveText.Compact(preferred.Select(p => p.Date)) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Ok(await LoadEntriesAsync(db, id, period, ct));
        });

        api.MapPost("/leave/parse", (ParseRequest req) =>
            LeaveText.TryParseDateList(req.Text, req.Reference, out var dates, out var error)
                ? new ParseResponse(dates, null)
                : new ParseResponse([], error));

        api.MapGet("/periods/{id:guid}/overview", async (Guid id, RotaDbContext db, LocalClock clock, HttpContext http, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();

            var holidays = await Mapping.HolidaysAsync(db, period.Id, ct);
            var left = await db.PeriodExclusions.Where(x => x.PeriodId == period.Id).Select(x => x.PersonId).ToListAsync(ct);
            var people = await db.People.Where(p => p.Status == OfficerStatus.OnCall && !left.Contains(p.Id))
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .Select(p => new
                {
                    p.Id, p.Code, p.Name,
                    Leave = p.Leave.Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate)
                        .OrderBy(l => l.Date)
                        .Select(l => new { l.Date, l.Note, l.ApprovalId })
                        .ToList(),
                    Preferred = p.PreferredDates.Where(d => d.Date >= period.StartDate && d.Date <= period.EndDate)
                        .OrderBy(d => d.Date).Select(d => d.Date).ToList(),
                })
                .ToListAsync(ct);

            // Other officers see only that leave is confirmed; the reason is for admins and the officer.
            var approvalIds = people.SelectMany(p => p.Leave).Where(l => l.ApprovalId != null).Select(l => l.ApprovalId!.Value).Distinct().ToList();
            var reasons = await db.LeaveApprovals.Where(a => approvalIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Reason, ct);
            bool admin = http.User.IsAdmin();
            Guid? me = http.User.PersonId();

            var onLeave = people.SelectMany(p => p.Leave.Select(l => l.Date)).GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
            var days = Mapping.Days(period.StartDate, period.EndDate)
                .Select(d => new OverviewDay(d, Mapping.IsWeekend(d) || holidays.ContainsKey(d), holidays.GetValueOrDefault(d),
                    people.Count - onLeave.GetValueOrDefault(d)))
                .ToList();

            return Results.Ok(new OverviewDto(
                period.ToDto(clock.Today), days,
                people.Select(p => new OverviewPerson(p.Id, p.Code, p.Name,
                    p.Leave.Select(l => l.ApprovalId is { } aid
                        ? new LeaveEntryDto(l.Date, admin || me == p.Id ? reasons.GetValueOrDefault(aid) : null, true)
                        : new LeaveEntryDto(l.Date, l.Note)).ToList(),
                    p.Preferred)).ToList()));
        });

        api.MapGet("/periods/{id:guid}/rota", async (Guid id, RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            // The published rota, or else the one in review.
            var run = await db.Runs.Include(r => r.Assignments).Where(r => r.PeriodId == id && (r.IsPublished || r.IsInReview))
                .OrderByDescending(r => r.IsPublished).FirstOrDefaultAsync(ct);
            if (period is null || run is null) return Results.NotFound();

            var names = await db.People.ToDictionaryAsync(p => p.Id, p => p.Name, ct);
            var holidays = await Mapping.HolidaysAsync(db, period.Id, ct);
            return Results.Ok(new PublishedRotaDto(period.ToDto(clock.Today), run.Id, Mapping.ToDays(run.Assignments, names, holidays),
                !run.IsPublished));
        });
    }

    private static bool CanAccess(HttpContext http, Guid personId) => http.User.IsAdmin() || http.User.PersonId() == personId;

    private static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static async Task<EntriesDto> LoadEntriesAsync(RotaDbContext db, Guid personId, RotaPeriod period, CancellationToken ct)
    {
        var leave = await db.LeaveDays
            .Where(l => l.PersonId == personId && l.ApprovalId == null && l.Date >= period.StartDate && l.Date <= period.EndDate)
            .OrderBy(l => l.Date).Select(l => new LeaveEntryDto(l.Date, l.Note, false)).ToListAsync(ct);
        var preferred = await db.PreferredDates
            .Where(p => p.PersonId == personId && p.Date >= period.StartDate && p.Date <= period.EndDate)
            .OrderBy(p => p.Date).Select(p => p.Date).ToListAsync(ct);
        var confirmed = await db.LeaveApprovals
            .Where(a => a.PersonId == personId && a.StartDate <= period.EndDate && a.EndDate >= period.StartDate)
            .OrderBy(a => a.StartDate)
            .Select(a => new ConfirmedLeaveDto(a.Id, a.StartDate, a.EndDate, a.Reason, a.Reference)).ToListAsync(ct);
        bool excluded = await db.PeriodExclusions.AnyAsync(x => x.PeriodId == period.Id && x.PersonId == personId, ct);
        return new EntriesDto(leave, preferred, confirmed, excluded);
    }
}

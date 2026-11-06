using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Leave;

namespace Rota.Api.Endpoints;

/// <summary>
/// Confirmed leave: absences already approved by the department (e.g. an overseas trip). Only admins enter it. It costs no
/// points itself and is never refused, but it reduces the officer's points and weekday allowance by the share of the
/// period it covers, and it counts toward a day being full for other officers. It can be entered before the period exists.
/// </summary>
public static class AdminLeaveApprovalEndpoints
{
    private const int MaxDays = 366, MaxRanges = 20;

    public static void MapLeaveApprovalEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/leave-approvals").WithTags("Admin confirmed leave");

        g.MapGet("/", async (Guid? personId, RotaDbContext db, CancellationToken ct) =>
        {
            var q = db.LeaveApprovals.AsQueryable();
            if (personId is { } pid) q = q.Where(a => a.PersonId == pid);
            var approvals = await q.OrderByDescending(a => a.StartDate).ToListAsync(ct);
            return await ToDtosAsync(db, approvals, ct);
        });

        g.MapPost("/", async (UpsertLeaveApprovalRequest req, RotaDbContext db, LeaveLimits limits, Notifier notify,
            HttpContext http, CancellationToken ct) =>
        {
            var approval = new LeaveApproval { CreatedByAccountId = http.User.AccountId() };
            return await SaveAsync(db, limits, notify, http, approval, req, isNew: true, ct);
        });

        // Several date ranges for one officer in one go (e.g. a trip split over three periods): one entry per range,
        // but a single notification and email.
        g.MapPost("/batch", async (BatchLeaveApprovalRequest req, RotaDbContext db, LeaveLimits limits, Notifier notify,
            HttpContext http, CancellationToken ct) => await SaveBatchAsync(db, limits, notify, http, req, ct));

        g.MapPut("/{id:guid}", async (Guid id, UpsertLeaveApprovalRequest req, RotaDbContext db, LeaveLimits limits,
            Notifier notify, HttpContext http, CancellationToken ct) =>
        {
            var approval = await db.LeaveApprovals.FindAsync([id], ct);
            if (approval is null) return Results.NotFound();
            if (approval.PersonId != req.PersonId)
                return Problem("person", "Confirmed leave can't be moved to another officer. Delete it and add it again.");
            return await SaveAsync(db, limits, notify, http, approval, req, isNew: false, ct);
        });

        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, Notifier notify, HttpContext http, CancellationToken ct) =>
        {
            var approval = await db.LeaveApprovals.FindAsync([id], ct);
            if (approval is null) return Results.NotFound();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockPeriodsAsync(db, approval.StartDate, approval.EndDate, ct);
            await db.LeaveDays.Where(l => l.ApprovalId == id).ExecuteDeleteAsync(ct);
            db.LeaveApprovals.Remove(approval);

            await notify.ToPeopleAsync([approval.PersonId], Notifier.ConfirmedLeave,
                $"Confirmed leave removed: {Range(approval.StartDate, approval.EndDate)}",
                "The admin removed this confirmed leave. You may be put on call on these days.", "/leave", email: true, ct);
            Mapping.Log(db, http, "confirmed-leave.delete", approval.PersonId, null, id, before: AuditOf(approval));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> SaveAsync(RotaDbContext db, LeaveLimits limits, Notifier notify, HttpContext http,
        LeaveApproval approval, UpsertLeaveApprovalRequest req, bool isNew, CancellationToken ct)
    {
        string? reason = Clean(req.Reason), reference = Clean(req.Reference);
        if (reason is null) return Problem("reason", "Give the reason, e.g. \"Overseas trip, approved by HOD\".");
        if (reason.Length > 200) return Problem("reason", "The reason can be at most 200 characters.");
        if (reference?.Length > 100) return Problem("reference", "The reference can be at most 100 characters.");
        if (req.EndDate < req.StartDate) return Problem("dates", "The end date is before the start date.");
        if (req.EndDate.DayNumber - req.StartDate.DayNumber + 1 > MaxDays)
            return Problem("dates", $"At most {MaxDays} days at a time. For a long absence, mark the officer as excluded.");

        var person = await db.People.FindAsync([req.PersonId], ct);
        if (person is null) return Results.NotFound();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Same lock as an officer saving leave, so nobody takes the last spot on a day while this is being saved.
        await LockPeriodsAsync(db, Min(req.StartDate, isNew ? req.StartDate : approval.StartDate),
            Max(req.EndDate, isNew ? req.EndDate : approval.EndDate), ct);

        var before = isNew ? null : AuditOf(approval);
        var oldRange = isNew ? null : Range(approval.StartDate, approval.EndDate);
        var warnings = new List<string>();
        var failure = await ApplyAsync(db, approval, person, req.StartDate, req.EndDate, reason, reference, req.Kind ?? approval.Kind, isNew, warnings, ct);
        if (failure is not null) return failure;

        string title = isNew
            ? $"Confirmed leave added: {Range(req.StartDate, req.EndDate)}"
            : $"Confirmed leave changed: {Range(req.StartDate, req.EndDate)}";
        string body = isNew
            ? $"Reason: {reason}\nYou won't be put on call on these days. It reduces your leave points and weekday allowance by the share of the rota it covers."
            : $"It was {oldRange}. Reason: {reason}";
        await notify.ToPeopleAsync([req.PersonId], Notifier.ConfirmedLeave, title, body, "/leave", email: true, ct);
        Mapping.Log(db, http, isNew ? "confirmed-leave.create" : "confirmed-leave.update", req.PersonId,
            new { officer = person.Name }, approval.Id, before, AuditOf(approval));
        await db.SaveChangesAsync(ct);

        warnings.AddRange(await PeriodWarningsAsync(db, limits, approval, person, ct));
        await tx.CommitAsync(ct);

        var dto = (await ToDtosAsync(db, [approval], ct)).Single();
        if (dto.Conflicts.Count > 0)
            warnings.Add($"{person.Name} is on call on {dto.Conflicts.Count} of these day(s) in a rota that's in review or published. " +
                         "Give those shifts to someone else.");
        return Results.Ok(new LeaveApprovalResultDto(dto, warnings));
    }

    /// <summary>Saves one range of confirmed leave (no transaction, lock or notification: the caller does those).</summary>
    internal static async Task<IResult?> ApplyAsync(RotaDbContext db, LeaveApproval approval, Person person, DateOnly start, DateOnly end,
        string reason, string? reference, LeaveKind kind, bool isNew, List<string> warnings, CancellationToken ct)
    {
        var overlap = await db.LeaveApprovals.Where(a => a.PersonId == person.Id && a.Id != approval.Id &&
                                                         a.StartDate <= end && a.EndDate >= start)
            .OrderBy(a => a.StartDate).FirstOrDefaultAsync(ct);
        if (overlap is not null)
            return Problem("dates", $"{person.Name} already has confirmed leave on {Range(overlap.StartDate, overlap.EndDate)}. " +
                                    "Change that one instead.");

        approval.PersonId = person.Id;
        approval.StartDate = start;
        approval.EndDate = end;
        approval.Reason = reason;
        approval.Reference = reference;
        approval.Kind = kind;
        approval.UpdatedAt = DateTimeOffset.UtcNow;
        if (isNew) db.LeaveApprovals.Add(approval);
        else await db.LeaveDays.Where(l => l.ApprovalId == approval.Id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);   // the approval row must exist before its days point at it

        // Leave the officer already marked on these days becomes confirmed (it no longer uses their points itself).
        var own = await db.LeaveDays.Where(l => l.PersonId == person.Id && l.ApprovalId == null &&
                                               l.Date >= start && l.Date <= end).ToListAsync(ct);
        foreach (var l in own) { l.ApprovalId = approval.Id; l.Note = null; }
        var taken = own.Select(l => l.Date).ToHashSet();
        db.LeaveDays.AddRange(Mapping.Days(start, end).Where(d => !taken.Contains(d))
            .Select(d => new LeaveDay { PersonId = person.Id, Date = d, ApprovalId = approval.Id }));
        if (own.Count > 0)
            warnings.Add($"{own.Count} day(s) {person.Name} had already marked as leave are now confirmed leave, so they no longer use points.");

        int preferred = await db.PreferredDates.Where(p => p.PersonId == person.Id && p.Date >= start && p.Date <= end)
            .ExecuteDeleteAsync(ct);
        if (preferred > 0) warnings.Add($"Removed {preferred} preferred on-call day(s) that fall on {Range(start, end)}.");

        if (person.Status != OfficerStatus.OnCall)
            warnings.Add($"{person.Name} is not on call at the moment ({person.StatusReason ?? person.Status.ToString()}).");
        return null;
    }

    private static async Task<IResult> SaveBatchAsync(RotaDbContext db, LeaveLimits limits, Notifier notify, HttpContext http,
        BatchLeaveApprovalRequest req, CancellationToken ct)
    {
        string? reason = Clean(req.Reason), reference = Clean(req.Reference);
        if (reason is null) return Problem("reason", "Give the reason, e.g. \"Overseas trip, approved by HOD\".");
        if (reason.Length > 200) return Problem("reason", "The reason can be at most 200 characters.");
        if (reference?.Length > 100) return Problem("reference", "The reference can be at most 100 characters.");
        var ranges = (req.Ranges ?? []).OrderBy(r => r.StartDate).ToList();
        if (ranges.Count == 0) return Problem("dates", "Add at least one date range.");
        if (ranges.Count > MaxRanges) return Problem("dates", $"At most {MaxRanges} date ranges at a time.");
        foreach (var r in ranges)
        {
            if (r.EndDate < r.StartDate) return Problem("dates", $"The range starting {LeaveText.Format(r.StartDate)} ends before it starts.");
            if (r.EndDate.DayNumber - r.StartDate.DayNumber + 1 > MaxDays)
                return Problem("dates", $"At most {MaxDays} days in one range. For a long absence, mark the officer as excluded.");
        }
        for (int i = 1; i < ranges.Count; i++)
            if (ranges[i].StartDate <= ranges[i - 1].EndDate)
                return Problem("dates", $"{Range(ranges[i - 1].StartDate, ranges[i - 1].EndDate)} and " +
                                        $"{Range(ranges[i].StartDate, ranges[i].EndDate)} overlap. Merge them into one range.");

        var person = await db.People.FindAsync([req.PersonId], ct);
        if (person is null) return Results.NotFound();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockPeriodsAsync(db, ranges[0].StartDate, ranges.Max(r => r.EndDate), ct);

        var warnings = new List<string>();
        var created = new List<LeaveApproval>();
        foreach (var r in ranges)
        {
            var approval = new LeaveApproval { CreatedByAccountId = http.User.AccountId() };
            var failure = await ApplyAsync(db, approval, person, r.StartDate, r.EndDate, reason, reference, req.Kind ?? LeaveKind.Other, isNew: true, warnings, ct);
            if (failure is not null) return failure;   // the transaction is rolled back on dispose
            created.Add(approval);
        }

        // One notification and email for the whole set.
        string list = string.Join(", ", created.Select(a => Range(a.StartDate, a.EndDate)));
        string title = $"Confirmed leave added: {list}";
        string body = $"Reason: {reason}\nYou won't be put on call on these days. It reduces your leave points and weekday allowance by 33% of what these days would have cost as ordinary leave.";
        await notify.ToPeopleAsync([req.PersonId], Notifier.ConfirmedLeave, title, body, "/leave", email: true, ct);
        foreach (var a in created)
            Mapping.Log(db, http, "confirmed-leave.create", req.PersonId, new { officer = person.Name }, a.Id, null, AuditOf(a));
        await db.SaveChangesAsync(ct);

        foreach (var a in created) warnings.AddRange(await PeriodWarningsAsync(db, limits, a, person, ct));
        await tx.CommitAsync(ct);

        var dtos = await ToDtosAsync(db, created, ct);
        int conflicts = dtos.Sum(d => d.Conflicts.Count);
        if (conflicts > 0)
            warnings.Add($"{person.Name} is on call on {conflicts} of these day(s) in a rota that's in review or published. " +
                         "Give those shifts to someone else.");
        return Results.Ok(new LeaveApprovalBatchResultDto(dtos.OrderBy(d => d.StartDate).ToList(), warnings.Distinct().ToList()));
    }

    /// <summary>Days pushed over the leave limit, a hint to exclude the officer instead, and drafts made before this leave.</summary>
    internal static async Task<List<string>> PeriodWarningsAsync(RotaDbContext db, LeaveLimits limits, LeaveApproval a, Person person,
        CancellationToken ct)
    {
        var warnings = new List<string>();
        var periods = await db.Periods.Where(p => p.StartDate <= a.EndDate && p.EndDate >= a.StartDate)
            .OrderBy(p => p.StartDate).ToListAsync(ct);
        var names = await db.People.ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        foreach (var period in periods)
        {
            var rules = await LeaveRules.LoadAsync(db, limits, period, ct);
            var off = rules.AllOff();
            DateOnly from = Max(a.StartDate, period.StartDate), to = Min(a.EndDate, period.EndDate);
            foreach (var d in Mapping.Days(from, to))
            {
                int count = off.GetValueOrDefault(d), cap = rules.Policy.DayCap(d, rules.OnCall);
                if (count <= cap) continue;
                var others = rules.OffOn(d, a.PersonId)
                    .Select(id => names.GetValueOrDefault(id, "?")).Order().ToList();
                warnings.Add($"{SwapEndpoints.Day(d)} now has {count} of {rules.OnCall} officers off (limit {cap}). " +
                             $"Also off: {string.Join(", ", others)}.");
            }

            if (rules.ConfirmedDays(a.PersonId) > 0 && person.Status == OfficerStatus.OnCall)
            {
                var own = rules.OwnLeave(a.PersonId);
                string text = $"{period.Name}: " + rules.DeductionText(a.PersonId, $"{person.Name}'s");
                if (rules.Policy.Points(own) > rules.OwnBudget(a.PersonId) || rules.Policy.Weekdays(own) > rules.OwnWeekdayAllowance(a.PersonId))
                    text += " They already have more leave than that. It stays, but they can't add more.";
                warnings.Add(text);
            }

            int periodDays = period.EndDate.DayNumber - period.StartDate.DayNumber + 1;
            int covered = to.DayNumber - from.DayNumber + 1;
            if (covered * 4 >= periodDays * 3)
                warnings.Add($"This covers {covered} of the {periodDays} days in {period.Name}. " +
                             $"If {person.Name} is away for most of it, consider marking them as excluded instead.");

            if (period.Status == PeriodStatus.Locked && await db.Runs.AnyAsync(r => r.PeriodId == period.Id && !r.IsPublished && !r.IsInReview, ct))
                warnings.Add($"Draft rotas for {period.Name} were made before this leave. Generate again so it's taken into account.");
        }
        return warnings;
    }

    internal static async Task<List<LeaveApprovalDto>> ToDtosAsync(RotaDbContext db, List<LeaveApproval> approvals, CancellationToken ct)
    {
        if (approvals.Count == 0) return [];
        var ids = approvals.Select(a => a.Id).ToList();
        var people = await db.People.AsNoTracking().ToListAsync(ct);
        var byId = people.ToDictionary(p => p.Id);
        var accountIds = approvals.Select(a => a.CreatedByAccountId).OfType<Guid>().Distinct().ToList();
        var creators = await db.Accounts.Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Email, a.PersonId }).ToListAsync(ct);
        var creatorName = creators.ToDictionary(c => c.Id,
            c => c.PersonId is { } pid && byId.TryGetValue(pid, out var p) ? p.Name : c.Email);

        // Shifts in published or in-review rotas on confirmed-leave days.
        var clashes = await (
            from l in db.LeaveDays
            where l.ApprovalId != null && ids.Contains(l.ApprovalId.Value)
            join s in db.Assignments on new { P = (Guid?)l.PersonId, l.Date } equals new { P = s.PersonId, s.Date }
            join r in db.Runs on s.RunId equals r.Id
            where r.IsPublished || r.IsInReview
            join p in db.Periods on r.PeriodId equals p.Id
            orderby l.Date
            select new { ApprovalId = l.ApprovalId!.Value, RunId = r.Id, PeriodId = p.Id, PeriodName = p.Name, l.Date, r.IsPublished })
            .ToListAsync(ct);

        var runIds = clashes.Select(c => c.RunId).Distinct().ToList();
        var assignments = await db.Assignments.Where(s => runIds.Contains(s.RunId))
            .Select(s => new { s.RunId, s.Date, s.PersonId }).ToListAsync(ct);
        var dates = clashes.Select(c => c.Date).Distinct().ToList();
        var leaveOn = (await db.LeaveDays.Where(l => dates.Contains(l.Date)).Select(l => new { l.PersonId, l.Date }).ToListAsync(ct))
            .Select(l => (l.PersonId, l.Date)).ToHashSet();
        var onCall = people.Where(p => p.Status == OfficerStatus.OnCall).ToList();

        List<ShiftCandidateDto> Candidates(Guid runId, DateOnly date)
        {
            var run = assignments.Where(s => s.RunId == runId).ToList();
            var works = run.Where(s => s.PersonId != null).Select(s => (s.PersonId!.Value, s.Date)).ToHashSet();
            var shifts = run.Where(s => s.PersonId != null).GroupBy(s => s.PersonId!.Value).ToDictionary(g => g.Key, g => g.Count());
            return onCall
                .Where(p => !leaveOn.Contains((p.Id, date)) && !works.Contains((p.Id, date)) &&
                            !works.Contains((p.Id, date.AddDays(-1))) && !works.Contains((p.Id, date.AddDays(1))))
                .Select(p => new ShiftCandidateDto(p.Id, p.Name, shifts.GetValueOrDefault(p.Id)))
                .OrderBy(c => c.Shifts).ThenBy(c => c.Name)
                .Take(8)
                .ToList();
        }

        return approvals.Select(a =>
        {
            var person = byId.GetValueOrDefault(a.PersonId);
            var conflicts = clashes.Where(c => c.ApprovalId == a.Id)
                .Select(c => new ApprovalConflictDto(c.RunId, c.PeriodId, c.PeriodName, c.Date, c.IsPublished, Candidates(c.RunId, c.Date)))
                .ToList();
            return new LeaveApprovalDto(a.Id, a.PersonId, person?.Code ?? "", person?.Name ?? "", a.StartDate, a.EndDate, a.Reason,
                a.Reference, a.CreatedByAccountId is { } cid ? creatorName.GetValueOrDefault(cid) : null, a.CreatedAt, conflicts, a.Kind);
        }).ToList();
    }

    /// <summary>Takes the per-period leave lock (see PUT /people/{id}/entries) of every period the dates touch.</summary>
    internal static async Task LockPeriodsAsync(RotaDbContext db, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ids = await db.Periods.Where(p => p.StartDate <= to && p.EndDate >= from).Select(p => p.Id).ToListAsync(ct);
        foreach (var id in ids.Order())
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", ct);
    }

    internal static object AuditOf(LeaveApproval a) => new { a.StartDate, a.EndDate, a.Reason, a.Reference, a.Kind };

    internal static string Range(DateOnly from, DateOnly to) =>
        from == to ? LeaveText.Format(from) : $"{LeaveText.Format(from)} – {LeaveText.Format(to)}";

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    internal static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    internal static IResult Problem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}

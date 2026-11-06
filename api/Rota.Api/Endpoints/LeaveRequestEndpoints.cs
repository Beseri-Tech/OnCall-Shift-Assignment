using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Api.Storage;
using Rota.Core.Leave;

namespace Rota.Api.Endpoints;

/// <summary>
/// Officers ask for confirmed leave (trip, CR, relief duty...) with one or more date ranges, a reason per range and an optional
/// document per range (uploaded straight to R2 through /api/documents first). The admin approves or rejects each range;
/// approving creates the confirmed leave.
/// </summary>
public static class LeaveRequestEndpoints
{
    private const int MaxDays = 366, MaxRanges = 20;
    internal const string DocumentParent = "ConfirmedLeaveRange";

    public static void MapLeaveRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/leave-requests").WithTags("Leave requests");

        // JSON: ranges with an optional documentId from a finished /api/documents upload (kind LeaveDocument).
        g.MapPost("/", async (LeaveRequestInput? input, RotaDbContext db, DocumentService docs, Notifier notify, HttpContext http,
            CancellationToken ct) =>
        {
            if (http.User.PersonId() is not { } personId) return Results.Forbid();
            var person = await db.People.FindAsync([personId], ct);
            if (person is null || person.Status != OfficerStatus.OnCall) return Results.Forbid();

            var ranges = input?.Ranges ?? [];
            if (ranges.Count == 0) return AdminLeaveApprovalEndpoints.Problem("dates", "Add at least one date range.");
            if (ranges.Count > MaxRanges) return AdminLeaveApprovalEndpoints.Problem("dates", $"At most {MaxRanges} date ranges at a time.");

            var order = Enumerable.Range(0, ranges.Count).OrderBy(i => ranges[i].StartDate).ToList();
            for (int n = 0; n < order.Count; n++)
            {
                var r = ranges[order[n]];
                if (r.EndDate < r.StartDate) return AdminLeaveApprovalEndpoints.Problem("dates", $"The range starting {LeaveText.Format(r.StartDate)} ends before it starts.");
                if (r.EndDate.DayNumber - r.StartDate.DayNumber + 1 > MaxDays)
                    return AdminLeaveApprovalEndpoints.Problem("dates", $"At most {MaxDays} days in one range.");
                if (AdminLeaveApprovalEndpoints.Clean(r.Reason) is not { } reason)
                    return AdminLeaveApprovalEndpoints.Problem("reason", $"Give a reason for {AdminLeaveApprovalEndpoints.Range(r.StartDate, r.EndDate)}.");
                if (reason.Length > 200) return AdminLeaveApprovalEndpoints.Problem("reason", "A reason can be at most 200 characters.");
                if (!Enum.IsDefined(r.Kind)) return AdminLeaveApprovalEndpoints.Problem("kind", "Pick the type of leave.");
                if (n > 0 && r.StartDate <= ranges[order[n - 1]].EndDate)
                    return AdminLeaveApprovalEndpoints.Problem("dates", "Two of the date ranges overlap. Merge them into one range.");
            }
            var docIds = ranges.Select(r => r.DocumentId).OfType<Guid>().ToList();
            if (docIds.Distinct().Count() != docIds.Count)
                return AdminLeaveApprovalEndpoints.Problem("document", "Each date range needs its own document.");

            var request = new LeaveRequest { PersonId = personId };
            foreach (var r in ranges)
                request.Ranges.Add(new LeaveRequestRange
                {
                    RequestId = request.Id, StartDate = r.StartDate, EndDate = r.EndDate, Kind = r.Kind,
                    Reason = AdminLeaveApprovalEndpoints.Clean(r.Reason)!, DocumentId = r.DocumentId,
                });

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.LeaveRequests.Add(request);
                await notify.ToAdminsAsync(Notifier.ConfirmedLeave, $"Leave request from {person.Name}",
                    string.Join("; ", request.Ranges.OrderBy(r => r.StartDate).Select(r => $"{AdminLeaveApprovalEndpoints.Range(r.StartDate, r.EndDate)} ({KindName(r.Kind)})")),
                    "/admin/leave", ct);
                Mapping.Log(db, http, "leave-request.create", personId, new { officer = person.Name, ranges = request.Ranges.Count }, request.Id, null, null);
                await db.SaveChangesAsync(ct);

                // Only the uploader can attach their own finished upload, and only once.
                foreach (var range in request.Ranges.Where(r => r.DocumentId is not null))
                {
                    var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == range.DocumentId, ct);
                    if (doc is null || doc.Kind != DocumentKind.LeaveDocument)
                        return AdminLeaveApprovalEndpoints.Problem("document", "That document wasn't uploaded as a leave document.");
                    await docs.AttachAsync(http.User, range.DocumentId!.Value, DocumentParent, range.Id.ToString(), ct);
                }
            }
            catch (DocumentException e)
            {
                return Results.Problem(e.Message, statusCode: e.Status);
            }
            await tx.CommitAsync(ct);
            return Results.Ok((await ToDtosAsync(db, request.Ranges.ToList(), ct)).OrderBy(r => r.StartDate).ToList());
        });

        g.MapGet("/mine", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (http.User.PersonId() is not { } personId) return Results.Ok(new List<LeaveRequestRangeDto>());
            var ranges = await db.LeaveRequestRanges.Where(r => db.LeaveRequests.Any(q => q.Id == r.RequestId && q.PersonId == personId))
                .ToListAsync(ct);
            return Results.Ok((await ToDtosAsync(db, ranges, ct)).OrderByDescending(r => r.SubmittedAt).ThenBy(r => r.StartDate).ToList());
        });

        // The officer withdraws a range nobody has decided on yet.
        g.MapDelete("/ranges/{id:guid}", async (Guid id, RotaDbContext db, DocumentService docs, HttpContext http, CancellationToken ct) =>
        {
            var range = await db.LeaveRequestRanges.FindAsync([id], ct);
            var request = range is null ? null : await db.LeaveRequests.FindAsync([range.RequestId], ct);
            if (range is null || request is null || request.PersonId != http.User.PersonId()) return Results.NotFound();
            if (range.Status != LeaveRequestStatus.Pending)
                return AdminLeaveApprovalEndpoints.Problem("status", "The admin has already decided on this request.");
            range.Status = LeaveRequestStatus.Cancelled;
            range.DecidedAt = DateTimeOffset.UtcNow;
            range.DocumentId = null;
            Mapping.Log(db, http, "leave-request.cancel", request.PersonId, null, range.Id, null, null);
            await db.SaveChangesAsync(ct);
            await docs.DeleteForParentAsync(DocumentParent, range.Id.ToString(), ct);
            return Results.NoContent();
        });

        var admin = app.MapGroup("/api/admin/leave-requests").RequireAuthorization(Session.AdminPolicy).WithTags("Admin leave requests");

        admin.MapGet("/", async (LeaveRequestStatus? status, RotaDbContext db, CancellationToken ct) =>
        {
            var q = db.LeaveRequestRanges.AsQueryable();
            if (status is { } s) q = q.Where(r => r.Status == s);
            var ranges = await q.ToListAsync(ct);
            return (await ToDtosAsync(db, ranges, ct)).OrderBy(r => r.Status == LeaveRequestStatus.Pending ? 0 : 1)
                .ThenByDescending(r => r.Status == LeaveRequestStatus.Pending ? DateTimeOffset.MinValue : r.SubmittedAt)
                .ThenBy(r => r.SubmittedAt).ThenBy(r => r.StartDate).ToList();
        });

        admin.MapPost("/ranges/{id:guid}/approve", async (Guid id, DecideLeaveRequestRequest? body, RotaDbContext db, LeaveLimits limits,
            Notifier notify, HttpContext http, CancellationToken ct) =>
        {
            var range = await db.LeaveRequestRanges.FindAsync([id], ct);
            var request = range is null ? null : await db.LeaveRequests.FindAsync([range.RequestId], ct);
            if (range is null || request is null) return Results.NotFound();
            var person = await db.People.FindAsync([request.PersonId], ct);
            if (person is null) return Results.NotFound();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await AdminLeaveApprovalEndpoints.LockPeriodsAsync(db, range.StartDate, range.EndDate, ct);
            await db.Entry(range).ReloadAsync(ct);
            if (range.Status != LeaveRequestStatus.Pending)
                return AdminLeaveApprovalEndpoints.Problem("status", "This request has already been decided or withdrawn.");

            var warnings = new List<string>();
            var approval = new LeaveApproval { CreatedByAccountId = http.User.AccountId() };
            var failure = await AdminLeaveApprovalEndpoints.ApplyAsync(db, approval, person, range.StartDate, range.EndDate, range.Reason,
                null, range.Kind, isNew: true, warnings, ct);
            if (failure is not null) return failure;

            range.Status = LeaveRequestStatus.Approved;
            range.ApprovalId = approval.Id;
            range.DecisionNote = AdminLeaveApprovalEndpoints.Clean(body?.Note);
            range.DecidedAt = DateTimeOffset.UtcNow;
            range.DecidedByAccountId = http.User.AccountId();
            string when = AdminLeaveApprovalEndpoints.Range(range.StartDate, range.EndDate);
            await notify.ToPeopleAsync([person.Id], Notifier.ConfirmedLeave, $"Leave request approved: {when}",
                "You won't be put on call on these days." + (range.DecisionNote is null ? "" : $"\nNote from the admin: {range.DecisionNote}"),
                "/leave-requests", email: true, ct);
            Mapping.Log(db, http, "leave-request.approve", person.Id, new { officer = person.Name }, range.Id, null,
                AdminLeaveApprovalEndpoints.AuditOf(approval));
            await db.SaveChangesAsync(ct);
            warnings.AddRange(await AdminLeaveApprovalEndpoints.PeriodWarningsAsync(db, limits, approval, person, ct));
            await tx.CommitAsync(ct);

            var conflicts = (await AdminLeaveApprovalEndpoints.ToDtosAsync(db, [approval], ct)).Single().Conflicts.Count;
            if (conflicts > 0)
                warnings.Add($"{person.Name} is on call on {conflicts} of these day(s) in a rota that's in review or published. Give those shifts to someone else on the Confirmed leave list.");
            return Results.Ok(new DecideLeaveRequestResult((await ToDtosAsync(db, [range], ct)).Single(), warnings.Distinct().ToList()));
        });

        admin.MapPost("/ranges/{id:guid}/reject", async (Guid id, DecideLeaveRequestRequest? body, RotaDbContext db, Notifier notify,
            HttpContext http, CancellationToken ct) =>
        {
            var range = await db.LeaveRequestRanges.FindAsync([id], ct);
            var request = range is null ? null : await db.LeaveRequests.FindAsync([range.RequestId], ct);
            if (range is null || request is null) return Results.NotFound();
            if (range.Status != LeaveRequestStatus.Pending)
                return AdminLeaveApprovalEndpoints.Problem("status", "This request has already been decided or withdrawn.");
            string? note = AdminLeaveApprovalEndpoints.Clean(body?.Note);
            if (note is null) return AdminLeaveApprovalEndpoints.Problem("note", "Give the officer a reason for rejecting.");
            if (note.Length > 200) return AdminLeaveApprovalEndpoints.Problem("note", "The note can be at most 200 characters.");

            range.Status = LeaveRequestStatus.Rejected;
            range.DecisionNote = note;
            range.DecidedAt = DateTimeOffset.UtcNow;
            range.DecidedByAccountId = http.User.AccountId();
            await notify.ToPeopleAsync([request.PersonId], Notifier.ConfirmedLeave,
                $"Leave request rejected: {AdminLeaveApprovalEndpoints.Range(range.StartDate, range.EndDate)}",
                $"Reason: {note}\nYou can mark your own leave as usual, or send a new request.", "/leave-requests", email: true, ct);
            Mapping.Log(db, http, "leave-request.reject", request.PersonId, new { note }, range.Id, null, null);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new DecideLeaveRequestResult((await ToDtosAsync(db, [range], ct)).Single(), []));
        });
    }

    internal static string KindName(LeaveKind k) => k switch
    {
        LeaveKind.Trip => "trip", LeaveKind.Cr => "CR", LeaveKind.Relief => "relief duty", _ => "other",
    };

    private static System.Text.Json.JsonSerializerOptions JsonOptions(HttpContext http) =>
        http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;

    private static async Task<List<LeaveRequestRangeDto>> ToDtosAsync(RotaDbContext db, List<LeaveRequestRange> ranges, CancellationToken ct)
    {
        if (ranges.Count == 0) return [];
        var requestIds = ranges.Select(r => r.RequestId).Distinct().ToList();
        var requests = await db.LeaveRequests.AsNoTracking().Where(r => requestIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var personIds = requests.Values.Select(r => r.PersonId).Distinct().ToList();
        var people = await db.People.AsNoTracking().Where(p => personIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var deciderIds = ranges.Select(r => r.DecidedByAccountId).OfType<Guid>().Distinct().ToList();
        var deciders = await db.Accounts.Where(a => deciderIds.Contains(a.Id)).Select(a => new { a.Id, a.Email, a.PersonId }).ToListAsync(ct);
        var allPeople = await db.People.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var deciderName = deciders.ToDictionary(d => d.Id, d => d.PersonId is { } pid && allPeople.TryGetValue(pid, out var n) ? n : d.Email);

        var docIds = ranges.Select(r => r.DocumentId).OfType<Guid>().ToList();
        var docs = docIds.Count == 0 ? [] : await db.Documents.AsNoTracking().Where(d => docIds.Contains(d.Id) && d.Status == DocumentStatus.Available)
            .ToDictionaryAsync(d => d.Id, ct);

        var pending = ranges.Where(r => r.Status == LeaveRequestStatus.Pending).ToList();
        var existing = pending.Count == 0 ? [] : await db.LeaveApprovals.AsNoTracking()
            .Where(a => personIds.Contains(a.PersonId)).ToListAsync(ct);

        return ranges.Select(r =>
        {
            var req = requests[r.RequestId];
            var p = people.GetValueOrDefault(req.PersonId);
            var overlaps = r.Status != LeaveRequestStatus.Pending ? [] : existing
                .Where(a => a.PersonId == req.PersonId && a.StartDate <= r.EndDate && a.EndDate >= r.StartDate)
                .Select(a => $"Already has confirmed leave on {AdminLeaveApprovalEndpoints.Range(a.StartDate, a.EndDate)}.").ToList();
            return new LeaveRequestRangeDto(r.Id, r.RequestId, req.PersonId, p?.Code ?? "", p?.Name ?? "", r.StartDate, r.EndDate, r.Kind,
                r.Reason, r.Status, r.DocumentId is { } did && docs.ContainsKey(did) ? did : null,
                r.DocumentId is { } d1 ? docs.GetValueOrDefault(d1)?.FileName : null, r.DocumentId is { } d2 ? docs.GetValueOrDefault(d2)?.SizeBytes : null, req.CreatedAt, r.DecisionNote,
                r.DecidedByAccountId is { } d ? deciderName.GetValueOrDefault(d) : null, r.DecidedAt, overlaps);
        }).ToList();
    }
}

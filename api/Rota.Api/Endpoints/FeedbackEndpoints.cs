using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

/// <summary>
/// Feedback about the app: any signed-in user sends it, admins read it, filter it and mark it resolved.
/// Sending and resolving are recorded in the audit log.
/// </summary>
public static class FeedbackEndpoints
{
    public const int MaxMessageLength = 2000;
    public const int MaxPerDay = 20;
    public const int MaxPageSize = 100;

    public static void MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/feedback").WithTags("Feedback");

        g.MapPost("/", async (SubmitFeedbackRequest req, RotaDbContext db, Notifier notifier, HttpContext http, CancellationToken ct) =>
        {
            string message = (req.Message ?? "").Trim();
            string? page = string.IsNullOrWhiteSpace(req.Page) ? null : req.Page.Trim();
            var errors = new Dictionary<string, string[]>();
            if (!Enum.IsDefined(req.Category)) errors["category"] = ["Choose a kind of feedback."];
            if (message.Length == 0) errors["message"] = ["Write your feedback."];
            else if (message.Length > MaxMessageLength) errors["message"] = [$"Keep it under {MaxMessageLength} characters."];
            if (page is { Length: > 200 }) page = page[..200];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var me = http.User.AccountId();
            var since = DateTimeOffset.UtcNow.AddDays(-1);
            if (await db.Feedback.CountAsync(f => f.AccountId == me && f.CreatedAt >= since, ct) >= MaxPerDay)
                return Results.Problem("You've sent a lot of feedback today. Please try again tomorrow.",
                    statusCode: StatusCodes.Status429TooManyRequests);

            var feedback = new Feedback { AccountId = me, Category = req.Category, Message = message, Page = page };
            db.Feedback.Add(feedback);
            await db.SaveChangesAsync(ct);   // for the id

            Mapping.Log(db, http, "feedback.create", http.User.PersonId(), null, feedback.Id,
                after: new { feedback.Category, feedback.Page, feedback.Message });
            string preview = message.Length > 200 ? message[..200] + "…" : message;
            await notifier.ToAdminsAsync(Notifier.Feedback, $"New feedback: {feedback.Category}", preview, "/admin/feedback", ct);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/feedback/{feedback.Id}", new { feedback.Id });
        });
    }

    public static void MapAdminFeedbackEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/feedback").WithTags("Admin feedback");

        // status: open | resolved (anything else = all). Newest first.
        g.MapGet("/", async (string? status, FeedbackCategory? category, int? page, int? pageSize, RotaDbContext db,
            CancellationToken ct) =>
        {
            var q = db.Feedback.AsNoTracking();
            if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase)) q = q.Where(f => f.ResolvedAt == null);
            else if (string.Equals(status, "resolved", StringComparison.OrdinalIgnoreCase)) q = q.Where(f => f.ResolvedAt != null);
            if (category is { } c) q = q.Where(f => f.Category == c);

            int size = Math.Clamp(pageSize ?? 25, 1, MaxPageSize), number = Math.Max(page ?? 1, 1);
            int total = await q.CountAsync(ct);
            int open = await db.Feedback.CountAsync(f => f.ResolvedAt == null, ct);
            var items = await q.OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id)
                .Skip((number - 1) * size).Take(size)
                .Select(f => new FeedbackDto(
                    f.Id, f.CreatedAt, f.Category, f.Message, f.Page, f.AccountId,
                    db.Accounts.Where(a => a.Id == f.AccountId).Select(a => a.Email).FirstOrDefault(),
                    db.Accounts.Where(a => a.Id == f.AccountId && a.Person != null).Select(a => a.Person!.Name).FirstOrDefault(),
                    f.ResolvedAt,
                    db.Accounts.Where(a => a.Id == f.ResolvedByAccountId).Select(a => a.Email).FirstOrDefault()))
                .ToListAsync(ct);
            return new FeedbackPageDto(items, total, number, size, open);
        });

        g.MapPost("/{id:long}/resolve", (long id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
            SetResolvedAsync(id, true, db, http, ct));
        g.MapPost("/{id:long}/reopen", (long id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
            SetResolvedAsync(id, false, db, http, ct));
    }

    private static async Task<IResult> SetResolvedAsync(long id, bool resolved, RotaDbContext db, HttpContext http, CancellationToken ct)
    {
        var f = await db.Feedback.FindAsync([id], ct);
        if (f is null) return Results.NotFound();
        if (f.ResolvedAt is not null == resolved) return Results.NoContent();   // already so: nothing changed, nothing logged

        var before = new { Status = f.ResolvedAt is null ? "Open" : "Resolved" };
        f.ResolvedAt = resolved ? DateTimeOffset.UtcNow : null;
        f.ResolvedByAccountId = resolved ? http.User.AccountId() : null;
        Mapping.Log(db, http, resolved ? "feedback.resolve" : "feedback.reopen", null, new { f.Category }, f.Id,
            before, new { Status = resolved ? "Resolved" : "Open" });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

/// <summary>
/// While a rota is in review, officers trade dates: the requester offers their date for the other officer's date,
/// and accepting swaps both shifts straight away. Everyone keeps the same number of shifts.
/// </summary>
public static class SwapEndpoints
{
    private const int MaxNoteLength = 200;

    public static void MapSwapEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/swaps").WithTags("Swaps");

        // Requests I sent or received (admins: all requests in rotas under review).
        g.MapGet("/", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var me = http.User.PersonId();
            var q = db.SwapRequests.AsQueryable();
            q = http.User.IsAdmin() && me is null
                ? q.Where(s => db.Runs.Any(r => r.Id == s.RunId && r.IsInReview))
                : q.Where(s => s.FromPersonId == me || s.ToPersonId == me);
            var swaps = await q.OrderByDescending(s => s.CreatedAt).Take(100).ToListAsync(ct);
            return await ToDtosAsync(db, swaps, ct);
        });

        g.MapPost("/", async (CreateSwapRequest req, RotaDbContext db, Notifier notify, LocalClock clock, HttpContext http,
            CancellationToken ct) =>
        {
            if (http.User.PersonId() is not { } me) return Results.Forbid();
            if (req.Note?.Length > MaxNoteLength) return Problem($"Notes can be at most {MaxNoteLength} characters.");

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var run = await LockRunAsync(db, req.RunId, ct);
            if (run is null) return Results.NotFound();

            var other = run.Assignments.FirstOrDefault(a => a.Date == req.ToDate)?.PersonId;
            if (other is null || other == me) return Problem("Pick a date someone else is on call.");
            if (Check(run, clock.Today, me, req.FromDate, other.Value, req.ToDate) is { } error) return Problem(error);
            if (await LeaveClashAsync(db, me, req.FromDate, other.Value, req.ToDate, ct) is { } clash) return Problem(clash);
            if (await db.SwapRequests.AnyAsync(s => s.RunId == run.Id && s.Status == SwapStatus.Pending &&
                    s.FromPersonId == me && s.FromDate == req.FromDate && s.ToDate == req.ToDate, ct))
                return Problem("You already asked for this swap.");

            var swap = new SwapRequest
            {
                RunId = run.Id, FromPersonId = me, FromDate = req.FromDate, ToPersonId = other.Value, ToDate = req.ToDate,
                Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            };
            db.SwapRequests.Add(swap);

            string fromName = await NameAsync(db, me, ct);
            await notify.ToPeopleAsync([other.Value], Notifier.SwapRequest, $"{fromName} asks to swap on-call dates",
                $"{fromName} would take your {Day(req.ToDate)} and give you {Day(req.FromDate)} ({run.Period!.Name})." +
                (swap.Note is null ? "" : $"\nNote: {swap.Note}"), "/my-oncall", email: true, ct);
            Mapping.Log(db, http, "swap.request", me, new
            {
                period = run.Period.Name, req.FromDate, req.ToDate, with = await NameAsync(db, other.Value, ct), swap.Note,
            }, swap.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok((await ToDtosAsync(db, [swap], ct))[0]);
        });

        g.MapPost("/{id:guid}/accept", async (Guid id, RotaDbContext db, Notifier notify, LocalClock clock, HttpContext http,
            CancellationToken ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var swap = await db.SwapRequests.FindAsync([id], ct);
            if (swap is null) return Results.NotFound();
            if (swap.ToPersonId != http.User.PersonId()) return Results.Forbid();

            var run = await LockRunAsync(db, swap.RunId, ct);
            await db.Entry(swap).ReloadAsync(ct);   // status may have changed while we waited for the lock
            if (swap.Status != SwapStatus.Pending) return Problem($"This request is already {swap.Status.ToString().ToLowerInvariant()}.");
            string? error = run is null ? "This rota no longer exists." : Check(run, clock.Today, swap.FromPersonId, swap.FromDate, swap.ToPersonId, swap.ToDate)
                ?? await LeaveClashAsync(db, swap.FromPersonId, swap.FromDate, swap.ToPersonId, swap.ToDate, ct);
            if (error is not null) return Problem(error);

            // The swap itself: each takes the other's day.
            var fromSlot = run!.Assignments.Single(a => a.Date == swap.FromDate);
            var toSlot = run.Assignments.Single(a => a.Date == swap.ToDate);
            (fromSlot.PersonId, toSlot.PersonId) = (swap.ToPersonId, swap.FromPersonId);
            fromSlot.IsManual = toSlot.IsManual = true;
            swap.Status = SwapStatus.Accepted;
            swap.RespondedAt = DateTimeOffset.UtcNow;

            string fromName = await NameAsync(db, swap.FromPersonId, ct), toName = await NameAsync(db, swap.ToPersonId, ct);
            await notify.ToPeopleAsync([swap.FromPersonId], Notifier.SwapAccepted, $"{toName} accepted your swap",
                $"You're now on call on {Day(swap.ToDate)} instead of {Day(swap.FromDate)} ({run.Period!.Name}).", "/my-oncall", email: false, ct);
            await notify.ToAdminsAsync(Notifier.SwapAccepted, $"Swap in {run.Period.Name}",
                $"{fromName} ({Day(swap.FromDate)}) and {toName} ({Day(swap.ToDate)}) swapped dates.", "/admin/rota", ct);
            await ExpireStaleAsync(db, run, notify, ct);
            Mapping.Log(db, http, "swap.accept", swap.ToPersonId, new { period = run.Period.Name }, id,
                new Dictionary<string, string> { [Day(swap.FromDate)] = fromName, [Day(swap.ToDate)] = toName },
                new Dictionary<string, string> { [Day(swap.FromDate)] = toName, [Day(swap.ToDate)] = fromName });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/decline", async (Guid id, RotaDbContext db, Notifier notify, HttpContext http, CancellationToken ct) =>
        {
            var swap = await db.SwapRequests.FindAsync([id], ct);
            if (swap is null) return Results.NotFound();
            if (swap.ToPersonId != http.User.PersonId()) return Results.Forbid();
            if (swap.Status != SwapStatus.Pending) return Results.NoContent();

            swap.Status = SwapStatus.Declined;
            swap.RespondedAt = DateTimeOffset.UtcNow;
            string toName = await NameAsync(db, swap.ToPersonId, ct);
            await notify.ToPeopleAsync([swap.FromPersonId], Notifier.SwapDeclined, $"{toName} declined your swap",
                $"You keep {Day(swap.FromDate)}; {toName} keeps {Day(swap.ToDate)}.", "/my-oncall", email: false, ct);
            Mapping.Log(db, http, "swap.decline", swap.ToPersonId, new { swap.FromDate, swap.ToDate, with = await NameAsync(db, swap.FromPersonId, ct) }, id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/cancel", async (Guid id, RotaDbContext db, Notifier notify, HttpContext http, CancellationToken ct) =>
        {
            var swap = await db.SwapRequests.FindAsync([id], ct);
            if (swap is null) return Results.NotFound();
            if (swap.FromPersonId != http.User.PersonId()) return Results.Forbid();
            if (swap.Status != SwapStatus.Pending) return Results.NoContent();

            swap.Status = SwapStatus.Cancelled;
            swap.RespondedAt = DateTimeOffset.UtcNow;
            string fromName = await NameAsync(db, swap.FromPersonId, ct);
            await notify.ToPeopleAsync([swap.ToPersonId], Notifier.SwapCancelled, $"{fromName} withdrew a swap request",
                $"The request to swap your {Day(swap.ToDate)} for {Day(swap.FromDate)} was withdrawn.", "/my-oncall", email: false, ct);
            Mapping.Log(db, http, "swap.cancel", swap.FromPersonId, new { swap.FromDate, swap.ToDate, with = await NameAsync(db, swap.ToPersonId, ct) }, id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Pending requests that no longer fit the rota (someone swapped or the admin changed a day) are closed,
    /// and both officers are told.
    /// </summary>
    internal static async Task ExpireStaleAsync(RotaDbContext db, RotaRun run, Notifier notify, CancellationToken ct,
        string reason = "The rota changed, so this swap is no longer possible.")
    {
        var owner = run.Assignments.ToDictionary(a => a.Date, a => a.PersonId);
        var pending = await db.SwapRequests.Where(s => s.RunId == run.Id && s.Status == SwapStatus.Pending).ToListAsync(ct);
        // Re-check in memory: a request accepted in this same request is still Pending in the database until saved.
        foreach (var s in pending.Where(s => s.Status == SwapStatus.Pending).Where(s => !run.IsInReview || owner.GetValueOrDefault(s.FromDate) != s.FromPersonId
                                             || owner.GetValueOrDefault(s.ToDate) != s.ToPersonId))
        {
            s.Status = SwapStatus.Expired;
            s.RespondedAt = DateTimeOffset.UtcNow;
            await notify.ToPeopleAsync([s.FromPersonId, s.ToPersonId], Notifier.SwapCancelled,
                $"Swap request closed ({Day(s.FromDate)} ↔ {Day(s.ToDate)})", reason, "/my-oncall", email: false, ct);
        }
    }

    /// <summary>Loads the run with its assignments, serializing swaps on it for the rest of the transaction.</summary>
    private static async Task<RotaRun?> LockRunAsync(RotaDbContext db, Guid runId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({runId.ToString()}, 1))", ct);
        return await db.Runs.Include(r => r.Assignments).Include(r => r.Period).FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    private static string? Check(RotaRun run, DateOnly today, Guid from, DateOnly fromDate, Guid to, DateOnly toDate)
    {
        if (!run.IsInReview) return "This rota is not open for swaps any more.";
        if (run.Period!.SwapDeadline is { } deadline && today > deadline)
            return $"Swaps closed on {Day(deadline)}. Ask the admin to change it.";
        if (fromDate == toDate || from == to) return "Pick two different dates and officers.";
        var owner = run.Assignments.ToDictionary(a => a.Date, a => a.PersonId);
        if (owner.GetValueOrDefault(fromDate) != from || owner.GetValueOrDefault(toDate) != to)
            return "The rota changed since this was requested; these dates no longer match.";
        return null;
    }

    private static async Task<string?> LeaveClashAsync(RotaDbContext db, Guid from, DateOnly fromDate, Guid to, DateOnly toDate,
        CancellationToken ct)
    {
        if (await db.LeaveDays.AnyAsync(l => l.PersonId == from && l.Date == toDate, ct))
            return $"{await NameAsync(db, from, ct)} is on leave on {Day(toDate)}.";
        if (await db.LeaveDays.AnyAsync(l => l.PersonId == to && l.Date == fromDate, ct))
            return $"{await NameAsync(db, to, ct)} is on leave on {Day(fromDate)}.";
        return null;
    }

    private static async Task<List<SwapDto>> ToDtosAsync(RotaDbContext db, List<SwapRequest> swaps, CancellationToken ct)
    {
        var runIds = swaps.Select(s => s.RunId).Distinct().ToList();
        var runs = await db.Runs.Include(r => r.Assignments).Include(r => r.Period).Where(r => runIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var names = await db.People.ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return swaps.Select(s =>
        {
            var run = runs[s.RunId];
            var warnings = s.Status == SwapStatus.Pending ? BackToBack(run, s, names) : [];
            return new SwapDto(s.Id, s.RunId, run.Period!.Name, s.FromPersonId, names.GetValueOrDefault(s.FromPersonId, "?"), s.FromDate,
                s.ToPersonId, names.GetValueOrDefault(s.ToPersonId, "?"), s.ToDate, s.Note, s.Status, s.CreatedAt, warnings);
        }).ToList();
    }

    /// <summary>Allowed, but worth knowing before accepting: the swap would give someone two days in a row.</summary>
    private static List<string> BackToBack(RotaRun run, SwapRequest s, Dictionary<Guid, string> names)
    {
        var warnings = new List<string>();
        void CheckFor(Guid person, DateOnly newDay, DateOnly givenUp)
        {
            if (run.Assignments.Any(a => a.PersonId == person && a.Date != givenUp && Math.Abs(a.Date.DayNumber - newDay.DayNumber) == 1))
                warnings.Add($"{names.GetValueOrDefault(person)} would work the day before or after {Day(newDay)}.");
        }
        CheckFor(s.FromPersonId, s.ToDate, s.FromDate);
        CheckFor(s.ToPersonId, s.FromDate, s.ToDate);
        return warnings;
    }

    private static async Task<string> NameAsync(RotaDbContext db, Guid personId, CancellationToken ct) =>
        await db.People.Where(p => p.Id == personId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? "An officer";

    internal static string Day(DateOnly d) => d.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    private static IResult Problem(string detail) => Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}

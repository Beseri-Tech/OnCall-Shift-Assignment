using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

/// <summary>Browse the audit log: who changed what and when, newest first.</summary>
public static class AdminAuditEndpoints
{
    public const int MaxPageSize = 200;

    public static void MapAuditEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/audit").WithTags("Admin audit");

        // from/to are days in the clinic's time zone, both included. action matches exactly; entity is the kind of thing
        // (person, clinic, period, run, ...); personId finds changes about one officer.
        g.MapGet("/", async (DateOnly? from, DateOnly? to, Guid? accountId, string? action, string? entity, Guid? personId,
            int? page, int? pageSize, RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var q = db.ChangeLog.AsNoTracking();
            if (from is { } f) { var start = clock.StartOf(f); q = q.Where(c => c.At >= start); }
            if (to is { } t) { var end = clock.StartOf(t.AddDays(1)); q = q.Where(c => c.At < end); }
            if (accountId is { } a) q = q.Where(c => c.AccountId == a);
            if (!string.IsNullOrWhiteSpace(action)) q = q.Where(c => c.Action == action);
            if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(c => c.Entity == entity);
            if (personId is { } p) q = q.Where(c => c.PersonId == p);

            int size = Math.Clamp(pageSize ?? 50, 1, MaxPageSize), number = Math.Max(page ?? 1, 1);
            int total = await q.CountAsync(ct);
            var rows = await q.OrderByDescending(c => c.At).ThenByDescending(c => c.Id)
                .Skip((number - 1) * size).Take(size)
                .Select(c => new
                {
                    Log = c,
                    Actor = db.Accounts.Where(x => x.Id == c.AccountId)
                        .Select(x => new { x.Email, Name = x.Person != null ? x.Person.Name : null }).FirstOrDefault(),
                    PersonName = db.People.Where(x => x.Id == c.PersonId).Select(x => x.Name).FirstOrDefault(),
                })
                .ToListAsync(ct);

            var items = rows.Select(r => new AuditEntryDto(
                r.Log.Id, r.Log.At, r.Log.AccountId, r.Actor?.Email, r.Actor?.Name, r.Log.Action, r.Log.Entity, r.Log.EntityId,
                r.Log.PersonId, r.PersonName, Parse(r.Log.Detail), Parse(r.Log.Before), Parse(r.Log.After), r.Log.Ip, r.Log.UserAgent))
                .ToList();
            return new AuditPageDto(items, total, number, size);
        });

        g.MapGet("/filters", async (RotaDbContext db, CancellationToken ct) =>
        {
            var actorIds = db.ChangeLog.Where(c => c.AccountId != null).Select(c => c.AccountId!.Value).Distinct();
            var actors = await db.Accounts.Where(a => actorIds.Contains(a.Id)).OrderBy(a => a.Email)
                .Select(a => new AuditActorDto(a.Id, a.Email, a.Person != null ? a.Person.Name : null)).ToListAsync(ct);
            var actions = await db.ChangeLog.Select(c => c.Action).Distinct().OrderBy(x => x).ToListAsync(ct);
            var entities = await db.ChangeLog.Where(c => c.Entity != null).Select(c => c.Entity!).Distinct().OrderBy(x => x).ToListAsync(ct);
            return new AuditFiltersDto(actors, actions, entities);
        });
    }

    private static JsonElement? Parse(string? json) => json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}

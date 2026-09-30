using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;

namespace Rota.Api.Endpoints;

/// <summary>The bell in the header: the signed-in account's latest notifications.</summary>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/notifications").WithTags("Notifications");

        g.MapGet("/", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var mine = db.Notifications.Where(n => n.AccountId == me);
            var items = await mine.OrderByDescending(n => n.CreatedAt).Take(30)
                .Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null))
                .ToListAsync(ct);
            return new NotificationsDto(items, await mine.CountAsync(n => n.ReadAt == null, ct));
        });

        g.MapPost("/{id:long}/read", async (long id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            await db.Notifications.Where(n => n.Id == id && n.AccountId == me && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
            return Results.NoContent();
        });

        g.MapPost("/read-all", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            await db.Notifications.Where(n => n.AccountId == me && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
            return Results.NoContent();
        });
    }
}

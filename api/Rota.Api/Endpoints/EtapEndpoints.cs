using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Reports;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

/// <summary>
/// ETAP claims: the officer's draft for a month, the filled-in PDF (only with a verified profile; never stored), and the
/// hospital's surat arahan.
/// </summary>
public static class EtapEndpoints
{
    public static void MapEtapEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/etap").WithTags("ETAP").AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(ctx);
        });

        g.MapGet("/", async (string month, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await MeAsync(db, http, ct) is not { } me) return OfficersOnly();
            if (!TryMonth(month, out int y, out int m)) return BadMonth();
            return Results.Ok(await Etap.BuildAsync(db, me, y, m, null, ct));
        });

        g.MapPut("/", async (string month, EtapSaveRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await MeAsync(db, http, ct) is not { } me) return OfficersOnly();
            if (!TryMonth(month, out int y, out int m)) return BadMonth();
            var rows = req.Rows ?? [];
            if (Etap.Validate(y, m, rows) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var claim = await db.EtapClaims.FirstOrDefaultAsync(c => c.PersonId == me.Id && c.Year == y && c.Month == m, ct);
            if (claim is null) db.EtapClaims.Add(claim = new EtapClaim { PersonId = me.Id, Year = y, Month = m });
            claim.Rows = Etap.WriteRows(rows);
            claim.UpdatedAt = DateTimeOffset.UtcNow;
            Mapping.Log(db, http, "etap.save", me.Id, new { month, days = rows.Count });
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Etap.BuildAsync(db, me, y, m, null, ct));
        });

        g.MapGet("/pdf", async (string month, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await MeAsync(db, http, ct) is not { } me) return OfficersOnly();
            if (!TryMonth(month, out int y, out int m)) return BadMonth();
            if (await ProfileEndpoints.VerifiedAsync(db, me, ct) is not { } profile)
                return Results.Problem("Verify your ETAP profile first (account menu → My ETAP profile).", statusCode: StatusCodes.Status400BadRequest);
            var claim = await Etap.BuildAsync(db, me, y, m, null, ct);
            if (claim.Rows.Count == 0) return Results.Problem("Add your on-call days and save first.", statusCode: StatusCodes.Status400BadRequest);
            byte[] pdf = await Task.Run(() => EtapPdf.Render(new EtapPdfData(profile, me, y, m, claim)), ct);
            Mapping.Log(db, http, "etap.generate", me.Id, new { month, days = claim.Rows.Count, claim.TotalAmount });
            await db.SaveChangesAsync(ct);
            return Results.File(pdf, "application/pdf", $"ETAP {month}.pdf");
        }).RequireRateLimiting(CaseEndpoints.DownloadLimit);

        // ---- surat arahan (admins) ----

        var admin = app.MapGroup("/api/admin/surat-arahan").WithTags("Surat arahan").RequireAuthorization(Session.AdminPolicy);
        admin.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
            await (from s in db.SuratArahan
                   join d in db.Documents on s.DocumentId equals d.Id
                   where d.Status == DocumentStatus.Available
                   orderby s.EffectiveFrom descending, s.CreatedAt descending
                   select new SuratArahanDto(s.Id, s.DocumentId, d.FileName, s.EffectiveFrom, s.Reference, s.CreatedAt)).ToListAsync(ct));

        admin.MapPost("/", async (AddSuratArahanRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == req.DocumentId, ct);
            if (doc is null || doc.Kind != DocumentKind.SuratArahan || doc.Status != DocumentStatus.Available)
                return Results.Problem("Upload the letter first.", statusCode: StatusCodes.Status400BadRequest);
            if (req.Reference?.Trim().Length > 100) return Results.Problem("Reference is too long.", statusCode: StatusCodes.Status400BadRequest);
            var s = new SuratArahan
            {
                DocumentId = doc.Id, EffectiveFrom = req.EffectiveFrom, CreatedById = http.User.AccountId(),
                Reference = string.IsNullOrWhiteSpace(req.Reference) ? null : req.Reference.Trim(),
            };
            db.SuratArahan.Add(s);
            doc.ParentType = "SuratArahan";
            doc.ParentId = s.Id.ToString();
            Mapping.Log(db, http, "surat-arahan.create", null, new { req.EffectiveFrom, s.Reference }, s.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { s.Id });
        });

        admin.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, Rota.Api.Storage.DocumentService docs, HttpContext http, CancellationToken ct) =>
        {
            var s = await db.SuratArahan.FindAsync([id], ct);
            if (s is null) return Results.NotFound();
            db.SuratArahan.Remove(s);
            Mapping.Log(db, http, "surat-arahan.delete", null, new { s.EffectiveFrom, s.Reference }, s.Id);
            await db.SaveChangesAsync(ct);
            await docs.DeleteAsync(http, s.DocumentId, ct);
            return Results.NoContent();
        });
    }

    private static async Task<Person?> MeAsync(RotaDbContext db, HttpContext http, CancellationToken ct) =>
        http.User.PersonId() is { } id ? await db.People.FirstOrDefaultAsync(p => p.Id == id, ct) : null;

    private static IResult OfficersOnly() => Results.Problem("ETAP is for officers on the rota.", statusCode: StatusCodes.Status403Forbidden);
    private static IResult BadMonth() => Results.Problem("Month is yyyy-MM.", statusCode: StatusCodes.Status400BadRequest);

    private static bool TryMonth(string? month, out int year, out int m)
    {
        year = m = 0;
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
        (year, m) = (d.Year, d.Month);
        return true;
    }
}

using Rota.Api.Data;
using Rota.Api.Storage;

namespace Rota.Api.Endpoints;

public sealed record UploadUrlRequest(DocumentKind Kind, string? FileName, string? ContentType, long FileSize);

/// <summary>
/// Document storage in private Cloudflare R2. The browser never sees R2 credentials: it asks here for a short-lived link,
/// sends the file straight to R2, then asks here to confirm it. See <see cref="DocumentService"/>.
/// </summary>
public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/documents").WithTags("Documents");

        g.MapPost("/upload-url", (UploadUrlRequest req, DocumentService docs, HttpContext http, CancellationToken ct) =>
            Run(async () => Results.Ok(await docs.RequestUploadAsync(http, req.Kind, req.FileName, req.ContentType, req.FileSize, ct))));

        g.MapPost("/{id:guid}/complete", (Guid id, DocumentService docs, HttpContext http, CancellationToken ct) =>
            Run(async () => Results.Ok(await docs.CompleteAsync(http, id, ct))));

        // The current user manual, or 204 when none has been uploaded yet.
        g.MapGet("/manual", (DocumentService docs, CancellationToken ct) =>
            Run(async () => await docs.CurrentManualAsync(ct) is { } m ? Results.Ok(m) : (IResult)Results.NoContent()));

        g.MapGet("/{id:guid}", (Guid id, DocumentService docs, HttpContext http, CancellationToken ct) =>
            Run(async () => Results.Ok(await docs.GetAsync(http.User, id, ct))));

        // inline=true opens PDFs/images in the browser instead of saving them.
        g.MapGet("/{id:guid}/download-url", (Guid id, bool? inline, DocumentService docs, HttpContext http, CancellationToken ct) =>
            Run(async () => Results.Ok(await docs.DownloadUrlAsync(http, id, inline ?? false, ct))));

        g.MapDelete("/{id:guid}", (Guid id, DocumentService docs, HttpContext http, CancellationToken ct) =>
            Run(async () => { await docs.DeleteAsync(http, id, ct); return Results.NoContent(); }));
    }

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (DocumentException e) { return Results.Problem(e.Message, statusCode: e.Status); }
    }
}

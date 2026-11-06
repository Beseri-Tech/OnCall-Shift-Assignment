using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Endpoints;

namespace Rota.Api.Storage;

/// <summary>A failure with the HTTP status and message to show the user.</summary>
public sealed class DocumentException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed record UploadTicket(Guid DocumentId, string UploadUrl, string Method, int ExpiresInSeconds,
    Dictionary<string, string> RequiredHeaders);

public sealed record DownloadTicket(string DownloadUrl, int ExpiresInSeconds);

public sealed record DocumentDto(Guid Id, DocumentKind Kind, string FileName, string ContentType, long SizeBytes,
    DateTimeOffset? UploadedAt, string? ParentType, string? ParentId);

/// <summary>
/// The reusable document API for the rest of the app (inject it, or call the /api/documents endpoints from the web app).
/// Uploads are two steps: <see cref="RequestUploadAsync"/> hands the browser a presigned link, the browser PUTs the file
/// straight to R2, then <see cref="CompleteAsync"/> checks the stored file and only then makes it downloadable.
/// Who may do what lives in <see cref="CanRead"/>, <see cref="CanDelete"/> and <see cref="CanUpload"/>.
/// </summary>
public sealed class DocumentService(RotaDbContext db, IDocumentStorage storage, IOptions<R2Options> options, TimeProvider clock)
{
    private const string QuarantinePrefix = "quarantine/";
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromDays(1);

    /// <summary>Extension → content type and the first bytes every genuine file of that type starts with.</summary>
    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> Types = new()
    {
        [".pdf"] = ("application/pdf", "%PDF-"u8.ToArray()),
        [".png"] = ("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        [".jpg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        [".jpeg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
    };

    public const string AllowedExtensions = "PDF, PNG, JPG or DOCX";

    private R2Options Options => options.Value;

    // ---- who may do what ----

    public static bool CanUpload(ClaimsPrincipal user, DocumentKind kind) => kind switch
    {
        DocumentKind.Manual => user.IsAdmin(),
        DocumentKind.LeaveDocument => user.PersonId() is not null || user.IsAdmin(),
        _ => true,
    };

    public static bool CanRead(ClaimsPrincipal user, DocumentRecord d) =>
        user.IsAdmin() || d.Kind == DocumentKind.Manual || IsOwner(user, d);

    public static bool CanDelete(ClaimsPrincipal user, DocumentRecord d) =>
        user.IsAdmin() || (d.Kind != DocumentKind.Manual && IsOwner(user, d));

    private static bool IsOwner(ClaimsPrincipal user, DocumentRecord d) =>
        (user.AccountId() is { } a && d.UploadedByAccountId == a) ||
        (user.PersonId() is { } p && d.OwnerPersonId == p);

    // ---- upload ----

    public async Task<UploadTicket> RequestUploadAsync(HttpContext http, DocumentKind kind, string? fileName,
        string? contentType, long fileSize, CancellationToken ct)
    {
        var user = http.User;
        RequireStorage();
        if (!Enum.IsDefined(kind)) throw new DocumentException(400, "Choose what kind of document this is.");
        if (!CanUpload(user, kind)) throw new DocumentException(403, "You can't upload this kind of document.");

        string name = CleanFileName(fileName);
        string ext = Path.GetExtension(name).ToLowerInvariant();
        if (!Types.TryGetValue(ext, out var type) || (kind == DocumentKind.Manual && ext != ".pdf"))
            throw new DocumentException(400, kind == DocumentKind.Manual
                ? "The manual must be a PDF file."
                : $"Only {AllowedExtensions} files can be uploaded.");
        if (!string.Equals(contentType, type.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new DocumentException(400, "The file type doesn't match its name.");
        long max = MaxBytes;
        if (fileSize <= 0) throw new DocumentException(400, "The file is empty.");
        if (fileSize > max) throw new DocumentException(400, $"The file is too large (limit {Options.MaxFileSizeMb} MB).");

        await DiscardStalePendingAsync(ct);

        var doc = new DocumentRecord
        {
            Kind = kind,
            FileName = name,
            ContentType = type.ContentType,
            SizeBytes = fileSize,
            UploadedByAccountId = user.AccountId(),
            OwnerPersonId = user.PersonId(),
        };
        doc.ObjectKey = $"{QuarantinePrefix}{doc.Id:N}";
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);

        var expiry = TimeSpan.FromMinutes(Math.Max(1, Options.UploadUrlExpiryMinutes));
        return new UploadTicket(doc.Id, storage.CreateUploadUrl(doc.ObjectKey, doc.ContentType, expiry), "PUT",
            (int)expiry.TotalSeconds, new() { ["Content-Type"] = doc.ContentType });
    }

    /// <summary>Checks what the browser stored, moves it to its permanent key and makes it downloadable. Safe to call twice.</summary>
    public async Task<DocumentDto> CompleteAsync(HttpContext http, Guid id, CancellationToken ct)
    {
        RequireStorage();
        var doc = await FindForUserAsync(http.User, id, ct, write: true);
        if (doc.Status == DocumentStatus.Available) return ToDto(doc);
        if (doc.Status != DocumentStatus.Pending) throw new DocumentException(409, "This upload was rejected or removed. Upload the file again.");

        var meta = await storage.GetMetadataAsync(doc.ObjectKey, ct);
        if (meta is null) throw new DocumentException(409, "The file hasn't arrived yet. Try again in a moment.");
        if (meta.Size > MaxBytes || meta.Size != doc.SizeBytes)
            throw await RejectAsync(http, doc, "The uploaded file doesn't match what was announced.", ct);

        string ext = Path.GetExtension(doc.FileName).ToLowerInvariant();
        byte[] sig = Types[ext].Signature;
        byte[] head = await storage.ReadHeadAsync(doc.ObjectKey, sig.Length, ct);
        if (!head.AsSpan().StartsWith(sig))
            throw await RejectAsync(http, doc, $"This isn't a real {ext.TrimStart('.').ToUpperInvariant()} file.", ct);

        // A new permanent key, so an unexpired upload link can't overwrite the approved file.
        string quarantineKey = doc.ObjectKey;
        string finalKey = $"documents/{doc.Kind.ToString().ToLowerInvariant()}/{Guid.NewGuid():N}{ext}";
        await storage.CopyAsync(quarantineKey, finalKey, ct);
        doc.ObjectKey = finalKey;
        doc.Status = DocumentStatus.Available;
        doc.UploadedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(quarantineKey, ct);

        if (doc.Kind == DocumentKind.Manual) await RetireOlderManualsAsync(doc, ct);

        Mapping.Log(db, http, "document.upload", doc.OwnerPersonId, new { doc.Kind, doc.FileName, doc.SizeBytes }, doc.Id);
        await db.SaveChangesAsync(ct);
        return ToDto(doc);
    }

    // ---- read ----

    public async Task<DownloadTicket> DownloadUrlAsync(HttpContext http, Guid id, bool inline, CancellationToken ct)
    {
        RequireStorage();
        var doc = await FindForUserAsync(http.User, id, ct);
        if (doc.Status != DocumentStatus.Available) throw new DocumentException(404, "Document not found.");
        var expiry = TimeSpan.FromMinutes(Math.Max(1, Options.DownloadUrlExpiryMinutes));
        return new DownloadTicket(storage.CreateDownloadUrl(doc.ObjectKey, doc.FileName, doc.ContentType, expiry, inline),
            (int)expiry.TotalSeconds);
    }

    public async Task<DocumentDto> GetAsync(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var doc = await FindForUserAsync(user, id, ct);
        if (doc.Status != DocumentStatus.Available) throw new DocumentException(404, "Document not found.");
        return ToDto(doc);
    }

    /// <summary>The current manual, or null when none has been uploaded.</summary>
    public async Task<DocumentDto?> CurrentManualAsync(CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking()
            .Where(d => d.Kind == DocumentKind.Manual && d.Status == DocumentStatus.Available)
            .OrderByDescending(d => d.UploadedAt).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDto(doc);
    }

    /// <summary>Available documents attached to a parent record that the user may read.</summary>
    public async Task<List<DocumentDto>> ListForParentAsync(ClaimsPrincipal user, string parentType, string parentId, CancellationToken ct)
    {
        var docs = await db.Documents.AsNoTracking()
            .Where(d => d.ParentType == parentType && d.ParentId == parentId && d.Status == DocumentStatus.Available)
            .OrderBy(d => d.CreatedAt).ToListAsync(ct);
        return docs.Where(d => CanRead(user, d)).Select(ToDto).ToList();
    }

    // ---- attach / delete ----

    /// <summary>
    /// Ties an uploaded document to a record (e.g. "ConfirmedLeaveRequest" and its id) once that record is saved.
    /// Only the uploader (or an admin) can attach, only finished uploads, and only once.
    /// </summary>
    public async Task AttachAsync(ClaimsPrincipal user, Guid id, string parentType, string parentId, CancellationToken ct)
    {
        var doc = await FindForUserAsync(user, id, ct, write: true);
        if (doc.Status != DocumentStatus.Available) throw new DocumentException(409, "The document hasn't finished uploading.");
        if (doc.ParentType is not null && (doc.ParentType != parentType || doc.ParentId != parentId))
            throw new DocumentException(409, "This document is already attached to something else.");
        doc.ParentType = parentType;
        doc.ParentId = parentId;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(HttpContext http, Guid id, CancellationToken ct)
    {
        RequireStorage();
        var doc = await FindForUserAsync(http.User, id, ct, write: true);
        if (doc.Status == DocumentStatus.Deleted) return;
        await RemoveAsync(doc, ct);
        Mapping.Log(db, http, "document.delete", doc.OwnerPersonId, new { doc.Kind, doc.FileName }, doc.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deletes every document attached to a parent (for when the parent record is removed).</summary>
    public async Task DeleteForParentAsync(string parentType, string parentId, CancellationToken ct)
    {
        var docs = await db.Documents.Where(d => d.ParentType == parentType && d.ParentId == parentId &&
                                                 d.Status != DocumentStatus.Deleted).ToListAsync(ct);
        foreach (var d in docs) await RemoveAsync(d, ct);
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    private long MaxBytes => Options.MaxFileSizeMb * 1024L * 1024L;

    private void RequireStorage()
    {
        if (!storage.IsEnabled)
            throw new DocumentException(503, "Document storage isn't set up yet. Ask the admin to configure Cloudflare R2.");
    }

    /// <summary>The document if the user may use it; 404 (not 403) otherwise so ids can't be probed.</summary>
    private async Task<DocumentRecord> FindForUserAsync(ClaimsPrincipal user, Guid id, CancellationToken ct, bool write = false)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        bool allowed = doc is not null && (write ? CanDelete(user, doc) || (doc.Status == DocumentStatus.Pending && IsOwner(user, doc))
                                                 : CanRead(user, doc));
        return allowed ? doc! : throw new DocumentException(404, "Document not found.");
    }

    private async Task RemoveAsync(DocumentRecord doc, CancellationToken ct)
    {
        if (storage.IsEnabled) await storage.DeleteAsync(doc.ObjectKey, ct);
        doc.Status = DocumentStatus.Deleted;
        doc.DeletedAt = clock.GetUtcNow();
    }

    private async Task<DocumentException> RejectAsync(HttpContext http, DocumentRecord doc, string reason, CancellationToken ct)
    {
        await RemoveAsync(doc, ct);
        Mapping.Log(db, http, "document.reject", doc.OwnerPersonId, new { doc.Kind, doc.FileName, reason }, doc.Id);
        await db.SaveChangesAsync(ct);
        return new DocumentException(422, reason);
    }

    private async Task RetireOlderManualsAsync(DocumentRecord current, CancellationToken ct)
    {
        var older = await db.Documents.Where(d => d.Kind == DocumentKind.Manual && d.Status == DocumentStatus.Available &&
                                                  d.Id != current.Id).ToListAsync(ct);
        foreach (var d in older) await RemoveAsync(d, ct);
    }

    /// <summary>Uploads that were never confirmed within a day: delete their leftover bytes and mark them removed.</summary>
    private async Task DiscardStalePendingAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow() - PendingLifetime;
        var stale = await db.Documents.Where(d => d.Status == DocumentStatus.Pending && d.CreatedAt < cutoff).Take(50).ToListAsync(ct);
        foreach (var d in stale) await RemoveAsync(d, ct);
        if (stale.Count > 0) await db.SaveChangesAsync(ct);
    }

    internal static string CleanFileName(string? fileName)
    {
        string name = Path.GetFileName((fileName ?? "").Replace('\\', '/')).Trim();
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        if (name.Length > 200) name = name[^200..];
        return name.Length == 0 ? "document" : name;
    }

    private static DocumentDto ToDto(DocumentRecord d) =>
        new(d.Id, d.Kind, d.FileName, d.ContentType, d.SizeBytes, d.UploadedAt, d.ParentType, d.ParentId);
}

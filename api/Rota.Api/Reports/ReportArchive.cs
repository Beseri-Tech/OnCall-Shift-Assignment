using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Api.Storage;
using Rota.Core.Leave;

namespace Rota.Api.Reports;

public sealed record ArchivedReportDto(Guid DocumentId, Guid RunId, string PeriodName, string FileName, long SizeBytes,
    DateTimeOffset CreatedAt, string? CreatedBy, bool RunIsPublished);

public sealed record UnarchivedRunDto(Guid RunId, string PeriodName, DateTimeOffset? PublishedAt);

public sealed record ReportArchiveDto(bool StorageEnabled, IReadOnlyList<ArchivedReportDto> Reports, IReadOnlyList<UnarchivedRunDto> Unarchived);

/// <summary>
/// Keeps a PDF report of every published rota in the private R2 bucket (as <see cref="DocumentKind.RotaReport"/> documents)
/// so the admin can download it later, even after the rota is unpublished or replaced. Each save is a new snapshot.
/// </summary>
public sealed class ReportArchive(RotaDbContext db, IDocumentStorage storage, LeaveLimits limits, LocalClock clock,
    TimeProvider time, ILogger<ReportArchive> log)
{
    public const string ParentType = "RotaRun";

    public bool Enabled => storage.IsEnabled;

    public async Task<string> NameOfAsync(Guid? accountId, CancellationToken ct) =>
        await db.Accounts.IgnoreQueryFilters().Where(a => a.Id == accountId).Select(a => a.Person != null ? a.Person.Name : a.Email).FirstOrDefaultAsync(ct)
        ?? "Admin";

    /// <summary>Builds the report and stores it. Returns null when the rota is gone or not published.</summary>
    public async Task<ArchivedReportDto?> SaveAsync(Guid runId, Guid? accountId, CancellationToken ct)
    {
        var run = await db.Runs.Include(r => r.Period).FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run?.Period is null || !run.IsPublished) return null;

        string by = await NameOfAsync(accountId, ct);
        var data = await RotaReportBuilder.BuildAsync(db, limits, clock, runId, by, ct);
        if (data is null) return null;
        byte[] pdf = await Task.Run(() => RotaReportPdf.Render(data), ct);

        var now = time.GetUtcNow();
        var doc = new DocumentRecord
        {
            Kind = DocumentKind.RotaReport,
            Status = DocumentStatus.Available,
            FileName = DocumentService.CleanFileName($"Rota report {run.Period.Name} ({clock.Format(now).Split(',')[0]}).pdf"),
            ContentType = "application/pdf",
            SizeBytes = pdf.Length,
            UploadedByAccountId = accountId,
            ParentType = ParentType,
            ParentId = runId.ToString(),
            CreatedAt = now,
            UploadedAt = now,
        };
        doc.ObjectKey = $"reports/{runId:N}/{doc.Id:N}.pdf";
        using (var ms = new MemoryStream(pdf)) await storage.PutAsync(doc.ObjectKey, ms, doc.ContentType, ct);
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);
        return new ArchivedReportDto(doc.Id, runId, run.Period.Name, doc.FileName, doc.SizeBytes, now, by, true);
    }

    /// <summary>Used when publishing: a failure here must never undo or block the publish, so it is only logged.</summary>
    public async Task TrySaveAsync(Guid runId, Guid? accountId, CancellationToken ct)
    {
        if (!storage.IsEnabled) return;
        try { await SaveAsync(runId, accountId, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError(e, "Could not save the PDF report for rota run {RunId}", runId);
        }
    }

    public async Task<ReportArchiveDto> ListAsync(CancellationToken ct)
    {
        var docs = await db.Documents.AsNoTracking()
            .Where(d => d.Kind == DocumentKind.RotaReport && d.Status == DocumentStatus.Available && d.ParentType == ParentType)
            .OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
        var runIds = docs.Select(d => Guid.TryParse(d.ParentId, out var g) ? g : Guid.Empty).Distinct().ToList();
        var runs = await db.Runs.AsNoTracking().Include(r => r.Period).Where(r => runIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var accountIds = docs.Where(d => d.UploadedByAccountId != null).Select(d => d.UploadedByAccountId!.Value).Distinct().ToList();
        var names = await db.Accounts.IgnoreQueryFilters().AsNoTracking().Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, Name = a.Person != null ? a.Person.Name : a.Email }).ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var reports = docs.Select(d =>
        {
            runs.TryGetValue(Guid.TryParse(d.ParentId, out var g) ? g : Guid.Empty, out var run);
            return new ArchivedReportDto(d.Id, run?.Id ?? Guid.Empty, run?.Period?.Name ?? "Deleted rota", d.FileName, d.SizeBytes,
                d.CreatedAt, d.UploadedByAccountId is { } a ? names.GetValueOrDefault(a) : null, run?.IsPublished == true);
        }).ToList();

        var archived = reports.Select(r => r.RunId).ToHashSet();
        var missing = (await db.Runs.AsNoTracking().Include(r => r.Period).Where(r => r.IsPublished).ToListAsync(ct))
            .Where(r => !archived.Contains(r.Id))
            .Select(r => new UnarchivedRunDto(r.Id, r.Period!.Name, r.PublishedAt)).ToList();

        return new ReportArchiveDto(storage.IsEnabled, reports, missing);
    }
}

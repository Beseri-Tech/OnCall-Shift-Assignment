using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Core.Excel;
using Rota.Core.Leave;
using Rota.Core.Names;

namespace Rota.Api.Endpoints;

/// <summary>
/// One-off imports from the old spreadsheets. Each is a stateless preview (parse + name matching)
/// followed by a commit of the rows the admin confirmed.
/// </summary>
public static class AdminImportEndpoints
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    public static void MapImportEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.WithTags("Admin imports");

        // Upload endpoints are protected by the SameSite=Strict admin cookie.
        g.MapPost("/periods/{id:guid}/import-leave", async (Guid id, IFormFile file, RotaDbContext db, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();
            if (ValidateUpload(file) is { } bad) return bad;

            LeaveSheet sheet;
            try
            {
                await using var stream = file.OpenReadStream();
                sheet = LeaveSheetReader.Read(stream, period.StartDate, period.EndDate);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
            {
                return Results.Problem($"Could not read the leave sheet: {ex.Message}", statusCode: StatusCodes.Status400BadRequest);
            }

            var people = await db.People.Where(p => p.Status == OfficerStatus.OnCall).ToListAsync(ct);
            var matches = NameMatcher.MatchAll(sheet.Rows.Select(r => r.RawName).ToList(), people, p => p.Name);

            var rows = sheet.Rows.Select((r, i) =>
            {
                var inPeriod = r.Leave.Where(kv => kv.Key >= period.StartDate && kv.Key <= period.EndDate)
                    .Select(kv => new LeaveEntryDto(kv.Key, kv.Value)).ToList();
                var warnings = r.Warnings.ToList();
                int outside = r.Leave.Count - inPeriod.Count;
                if (outside > 0) warnings.Add($"{outside} date(s) outside {period.Name} ignored");

                var m = matches[i];
                return new LeaveImportRow(r.RawName, m.Match?.Id, m.Match?.Name, m.Type, inPeriod, warnings);
            }).ToList();

            return Results.Ok(new LeaveImportPreview(sheet.Months, rows));
        }).DisableAntiforgery();

        g.MapPost("/periods/{id:guid}/import-leave/commit", async (Guid id, LeaveImportCommit req, RotaDbContext db,
            HttpContext http, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();
            if (period.Status == PeriodStatus.Published)
                return Results.Problem("This period's rota is published; unpublish it before importing leave.",
                    statusCode: StatusCodes.Status409Conflict);

            var people = await db.People.ToDictionaryAsync(p => p.Id, ct);
            var names = people.Values.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var warnings = new List<string>();
            var cleared = new HashSet<Guid>();
            int updated = 0, added = 0, skipped = 0;

            var newRows = req.Rows.Where(r => r.Action == ImportAction.New).ToList();
            var codes = await AdminEndpoints.NextCodeAsync(db, newRows.Count, ct);
            int nextOrder = (people.Values.Select(p => (int?)p.SortOrder).Max() ?? -1) + 1;

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            foreach (var row in req.Rows)
            {
                Person? person;
                switch (row.Action)
                {
                    case ImportAction.Skip:
                        skipped++;
                        continue;

                    case ImportAction.New:
                        string name = row.RawName.Trim();
                        if (names.Contains(name))
                        {
                            warnings.Add($"{name} already exists; skipped.");
                            skipped++;
                            continue;
                        }
                        person = new Person { Name = name, Code = codes[added], SortOrder = nextOrder + added };
                        db.People.Add(person);
                        people[person.Id] = person;
                        names.Add(name);
                        added++;
                        break;

                    default:
                        if (row.PersonId is not { } pid || !people.TryGetValue(pid, out person))
                        {
                            warnings.Add($"{row.RawName}: person not found; skipped.");
                            skipped++;
                            continue;
                        }
                        updated++;
                        break;
                }

                // Replace leave inside this period; leave elsewhere is untouched.
                if (cleared.Add(person.Id) && row.Action != ImportAction.New)
                    await db.LeaveDays.Where(l => l.PersonId == person.Id && l.Date >= period.StartDate && l.Date <= period.EndDate)
                        .ExecuteDeleteAsync(ct);

                var tracked = db.ChangeTracker.Entries<LeaveDay>().Where(e => e.Entity.PersonId == person.Id)
                    .Select(e => e.Entity.Date).ToHashSet();
                foreach (var l in row.Leave.Where(l => l.Date >= period.StartDate && l.Date <= period.EndDate && !tracked.Contains(l.Date))
                             .GroupBy(l => l.Date).Select(g => g.First()))
                    db.LeaveDays.Add(new LeaveDay { PersonId = person.Id, Date = l.Date, Note = l.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 200)] : null });
            }

            Mapping.Log(db, http, "import.leave", null, new { period = period.Name, updated, added, skipped });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new ImportResult(updated, added, skipped, warnings));
        });

        g.MapPost("/people/import-opening", async (IFormFile file, RotaDbContext db, CancellationToken ct) =>
        {
            if (ValidateUpload(file) is { } bad) return bad;

            List<WeekendHistoryRow> sheetRows;
            try
            {
                await using var stream = file.OpenReadStream();
                sheetRows = WeekendHistorySheet.Read(stream);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
            {
                return Results.Problem($"Could not read the history sheet: {ex.Message}", statusCode: StatusCodes.Status400BadRequest);
            }

            var people = await db.People.Where(p => p.Status == OfficerStatus.OnCall).ToListAsync(ct);
            var matches = NameMatcher.MatchAll(sheetRows.Select(r => r.Name).ToList(), people, p => p.Name);
            var rows = sheetRows.Select((r, i) =>
                new OpeningImportRow(r.Name, r.WeekendHolidayShifts, matches[i].Match?.Id, matches[i].Match?.Name, matches[i].Type)).ToList();

            var matched = matches.Where(m => m.Match != null).Select(m => m.Match!.Id).ToHashSet();
            var withoutRow = people.Where(p => !matched.Contains(p.Id)).Select(p => p.Name).ToList();
            return Results.Ok(new OpeningImportPreview(rows, withoutRow));
        }).DisableAntiforgery();

        g.MapPost("/people/import-opening/commit", async (OpeningImportCommit req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var people = await db.People.ToDictionaryAsync(p => p.Id, ct);
            int updated = 0;
            foreach (var row in req.Rows)
            {
                if (!people.TryGetValue(row.PersonId, out var p) || row.OpeningWeekendHoliday < 0) continue;
                p.OpeningWeekendHoliday = row.OpeningWeekendHoliday;
                p.UpdatedAt = DateTimeOffset.UtcNow;
                updated++;
            }

            Mapping.Log(db, http, "import.opening", null, new { updated });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ImportResult(updated, 0, req.Rows.Count - updated, []));
        });
    }

    private static IResult? ValidateUpload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return Results.Problem("Choose an .xlsx file.", statusCode: StatusCodes.Status400BadRequest);
        if (file.Length > MaxUploadBytes)
            return Results.Problem("The file is larger than 5 MB.", statusCode: StatusCodes.Status400BadRequest);
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Results.Problem("Only .xlsx files are supported.", statusCode: StatusCodes.Status400BadRequest);
        return null;
    }
}

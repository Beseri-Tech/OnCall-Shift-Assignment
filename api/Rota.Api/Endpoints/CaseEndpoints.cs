using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Excel;

namespace Rota.Api.Endpoints;

/// <summary>
/// The on-call case log ("Buku Oncall"): one entry per patient seen on call. Everyone in the hospital reads and downloads
/// it; the recorder and admins change it. Patient data is sensitive, so: hospital-scoped by the query filter, never cached
/// (no-store), never in URLs, notifications or file names, and the audit log records who looked or changed what, by case id
/// only - never the patient's details.
/// </summary>
public static partial class CaseEndpoints
{
    public const string DownloadLimit = "case-download";
    public const int MaxImportRows = 5000;

    public static readonly string[] Genders = ["MALE", "FEMALE"];
    public static readonly string[] Races = ["MALAY", "CHINESE", "INDIAN", "SIAMESE"];
    public static readonly string[] Referrals = ["ED", "WARD", "OT", "OSCC", "FORENSIC/MORTUARY"];
    public static readonly string[] Departments = ["OMFS", "PAEDS"];
    public static readonly string[] DsaStatuses = ["CALLED AND ATTENDED", "CALLED BUT FTA", "CALLED BUT NO ANSWER", "NOT BEEN CALLED"];

    /// <summary>A case starting before this belongs to the previous day's on-call shift.</summary>
    private static readonly TimeOnly ShiftEnd = new(8, 0);

    public static void MapCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases").WithTags("Cases").AddEndpointFilter(NoStore);

        // month: yyyy-MM of the on-call date (default: this month). personId: one officer's cases. q: free-text search.
        g.MapGet("/", async (string? month, Guid? personId, string? q, RotaDbContext db, LocalClock clock, HttpContext http,
            CancellationToken ct) =>
        {
            if (ParseMonth(month, clock.Today) is not var (from, to)) return Results.Problem("Month is yyyy-MM.", statusCode: 400);
            var rows = await ListAsync(db, http, from, to, personId, q, ct);
            Mapping.Log(db, http, "case.view-list", personId, new { month = $"{from:yyyy-MM}", count = rows.Count });
            await db.SaveChangesAsync(ct);
            return Results.Ok(rows);
        });

        // One month (month=yyyy-MM) or a whole year (year=yyyy).
        g.MapGet("/export.xlsx", async (string? month, int? year, Guid? personId, RotaDbContext db, LocalClock clock, HttpContext http,
            CancellationToken ct) =>
        {
            DateOnly from, to;
            if (year is { } y)
            {
                if (y is < 2000 or > 2100) return Results.Problem("Year is yyyy.", statusCode: 400);
                (from, to) = (new DateOnly(y, 1, 1), new DateOnly(y + 1, 1, 1));
            }
            else if (ParseMonth(month, clock.Today) is var (f, t)) (from, to) = (f, t);
            else return Results.Problem("Month is yyyy-MM.", statusCode: 400);
            string label = year is null ? $"{from:yyyy-MM}" : $"{from:yyyy}";
            var rows = await ListAsync(db, http, from, to, personId, null, ct);
            Mapping.Log(db, http, "case.download", personId, new { period = label, count = rows.Count });
            await db.SaveChangesAsync(ct);
            return Results.File(ExcelExports.Table("Cases", ExportHeaders, rows.OrderBy(r => r.OnCallDate).ThenBy(r => r.Date)
                    .ThenBy(r => r.TimeStart).Select(ExportRow)),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Oncall cases {label}.xlsx");
        }).RequireRateLimiting(DownloadLimit);

        // The WhatsApp passover text for the department group (copied by the officer; recorded like a view).
        g.MapGet("/{id:guid}/whatsapp", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var c = await db.Cases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return Results.NotFound();
            if (!IsOwn(http, c)) return Results.Forbid();
            Mapping.Log(db, http, "case.whatsapp", c.PersonId, new { c.OnCallDate }, c.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CaseTextDto(CaseText.WhatsApp(c)));
        });

        g.MapPost("/", async (CaseInput input, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (http.User.AccountId() is not { } me || http.User.FacilityId() is null) return Results.Forbid();
            var (officer, problem) = await OfficerAsync(db, http, input, null, ct);
            if (problem is not null) return problem;
            var c = new OnCallCase { RecordedById = me };
            if (Apply(c, input, officer, strict: true) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            db.Cases.Add(c);
            Mapping.Log(db, http, "case.create", c.PersonId, new { c.OnCallDate }, c.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { c.Id });
        });

        g.MapPut("/{id:guid}", async (Guid id, CaseInput input, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var c = await db.Cases.FindAsync([id], ct);
            if (c is null) return Results.NotFound();
            if (!CanEdit(http, c)) return Results.Forbid();
            var (officer, problem) = await OfficerAsync(db, http, input, c.PersonId, ct);
            if (problem is not null) return problem;
            var before = new { c.OnCallDate, c.PersonId };
            if (Apply(c, input, officer, strict: true) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            c.UpdatedAt = DateTimeOffset.UtcNow;
            Mapping.Log(db, http, "case.update", c.PersonId, null, c.Id, before, new { c.OnCallDate, c.PersonId });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var c = await db.Cases.FindAsync([id], ct);
            if (c is null) return Results.NotFound();
            if (!CanEdit(http, c)) return Results.Forbid();
            c.DeletedAt = DateTimeOffset.UtcNow;
            Mapping.Log(db, http, "case.delete", c.PersonId, new { c.OnCallDate }, c.Id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // The Google Sheet of form responses, exported as CSV. Checked first; nothing is saved unless every row is valid.
        g.MapPost("/import", async (CaseImportRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var (rows, fileErrors, duplicates) = await CheckImportAsync(db, req, http.User.AccountId()!.Value, ct);
            bool ok = req.Commit && fileErrors.Count == 0 && rows.Count > 0 && rows.All(r => r.Errors.Count == 0);
            if (ok)
            {
                foreach (var r in rows.Where(r => !r.Duplicate)) db.Cases.Add(r.Case!);
                Mapping.Log(db, http, "case.import", null, new { added = rows.Count(r => !r.Duplicate), duplicates });
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new CaseImportResultDto(
                rows.Select(r => new CaseImportRowDto(r.Line, r.Case?.Date, r.Officer, r.Duplicate, r.Errors)).ToList(),
                fileErrors, ok, ok ? rows.Count(r => !r.Duplicate) : 0, duplicates));
        }).RequireAuthorization(Session.AdminPolicy);
    }

    private static async ValueTask<object?> NoStore(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        ctx.HttpContext.Response.Headers.CacheControl = "no-store";
        ctx.HttpContext.Response.Headers.Pragma = "no-cache";
        return await next(ctx);
    }

    /// <summary>Admins, whoever typed it in, and the officer in charge.</summary>
    private static bool CanEdit(HttpContext http, OnCallCase c) => http.User.IsAdmin() || IsOwn(http, c);

    /// <summary>The signed-in user recorded it or is its officer in charge (only they make its WhatsApp passover; admins too only for their own).</summary>
    private static bool IsOwn(HttpContext http, OnCallCase c) => http.User.AccountId() == c.RecordedById || http.User.PersonId() == c.PersonId;

    /// <summary>Whoever the published (or in-review) rota has on call on the date.</summary>
    internal static async Task<OnCallOfficerDto?> OnCallOfficerAsync(RotaDbContext db, DateOnly date, CancellationToken ct) =>
        await (from a in db.Assignments
               join r in db.Runs on a.RunId equals r.Id
               join p in db.People on a.PersonId equals p.Id
               where a.Date == date && (r.IsPublished || r.IsInReview)
               orderby r.IsPublished descending
               select new OnCallOfficerDto(p.Id, p.Name)).FirstOrDefaultAsync(ct);

    /// <summary>
    /// The officer in charge: the officer typing it in (when editing, <paramref name="current"/> stays as it was). An explicit
    /// OfficerId is still accepted for admins. Must be on this hospital's rota.
    /// </summary>
    private static async Task<(Guid Officer, IResult? Problem)> OfficerAsync(RotaDbContext db, HttpContext http, CaseInput input,
        Guid? current, CancellationToken ct)
    {
        Guid? officer = input.OfficerId ?? current ?? http.User.PersonId();
        if (officer is not { } id || !await db.Officers.AnyAsync(p => p.Id == id, ct))
            return (Guid.Empty, Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["officerId"] = ["Only officers on the rota record on-call cases."],
            }));
        return (id, null);
    }

    private static (DateOnly From, DateOnly To)? ParseMonth(string? month, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(month)) return (new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, 1).AddMonths(1));
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)) return null;
        return (first, first.AddMonths(1));
    }

    private static async Task<List<CaseDto>> ListAsync(RotaDbContext db, HttpContext http, DateOnly from, DateOnly to, Guid? personId,
        string? q, CancellationToken ct)
    {
        var query = db.Cases.AsNoTracking().Where(c => c.OnCallDate >= from && c.OnCallDate < to);
        if (personId is { } pid) query = query.Where(c => c.PersonId == pid);
        var cases = await query.OrderByDescending(c => c.OnCallDate).ThenByDescending(c => c.Date).ThenByDescending(c => c.TimeStart)
            .ToListAsync(ct);
        var ids = cases.Select(c => c.PersonId).Concat(cases.Select(c => c.RecordedById)).Distinct().ToList();
        // Recorders may be masters (no hospital): names come from outside the hospital filter.
        var names = await db.People.IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        string s = q?.Trim().ToUpperInvariant() ?? "";
        return cases
            .Where(c => s.Length == 0 || c.PatientName.Contains(s) || c.PatientIdNo.Contains(s)
                || (c.Diagnosis?.ToUpperInvariant().Contains(s) ?? false) || names.GetValueOrDefault(c.PersonId, "").ToUpperInvariant().Contains(s))
            .Select(c => new CaseDto(c.Id, c.PersonId, names.GetValueOrDefault(c.PersonId, "?"), names.GetValueOrDefault(c.RecordedById, "?"),
                c.OnCallDate, c.Date, c.TimeStart, c.TimeFinish, c.Minutes, c.PatientName, c.PatientIdNo, c.Gender, c.Age, c.Race,
                c.ReferralSource, c.Department, c.Diagnosis, c.Management, c.Plan, c.Taggers, c.DsaName, c.DsaStatus, c.Confidential,
                CanEdit(http, c), IsOwn(http, c), c.UpdatedAt, c.SpokenTo, c.SeenBy, c.CaseType, c.Presentation, c.Investigations, c.Prescription, c.EdZone))
            .ToList();
    }

    private static readonly string[] ExportHeaders =
    [
        "On-call date", "Date", "Time start", "Time finish", "Duration (h:mm)", "Patient name", "MyKad/KID/Passport/Reg no",
        "Gender", "Age", "Race", "Source of referral", "ED zone", "Department", "Diagnosis", "Management", "Plan",
        "S/T", "S/B", "Case type", "P/W or C/O", "Ix", "Rx", "Officer incharge", "Taggers", "DSA oncall", "DSA status", "Recorded by", "Confidential",
    ];

    private static IReadOnlyList<string?> ExportRow(CaseDto c) =>
    [
        c.OnCallDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), c.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        c.TimeStart.ToString("HH:mm", CultureInfo.InvariantCulture), c.TimeFinish.ToString("HH:mm", CultureInfo.InvariantCulture),
        $"{c.Minutes / 60}:{c.Minutes % 60:00}", c.PatientName, c.PatientIdNo, c.Gender, c.Age, c.Race, c.ReferralSource, c.EdZone,
        c.Department, c.Diagnosis, c.Management, c.Plan, c.SpokenTo, c.SeenBy, c.CaseType, c.Presentation, c.Investigations,
        c.Prescription, c.Officer, c.Taggers, c.DsaName, c.DsaStatus, c.RecordedBy,
        c.Confidential ? "Yes" : "",
    ];

    /// <summary>
    /// Checks and copies the input onto the case (with the resolved officer in charge); returns the problems by field (nothing
    /// is changed when there are any). strict: the form's rules (department, case type, DSA name and status required); the old sheet import is lenient.
    /// </summary>
    internal static Dictionary<string, string[]> Apply(OnCallCase c, CaseInput i, Guid officer, bool strict)
    {
        var errors = new Dictionary<string, string[]>();
        string? dsaStatus = Upper(i.DsaStatus);
        if (strict && Clean(i.DsaName) is null) errors["dsaName"] = ["Enter the DSA on call."];
        if (dsaStatus is not null && !DsaStatuses.Contains(dsaStatus)) errors["dsaStatus"] = ["Choose one of the DSA options."];
        else if (strict && dsaStatus is null) errors["dsaStatus"] = ["Choose whether the DSA was called."];
        string name = Upper(i.PatientName) ?? "";
        string idNo = NormalizeIdNo(i.PatientIdNo);
        string gender = Upper(i.Gender) ?? "";
        string? department = Upper(i.Department);
        var onCall = i.OnCallDate ?? DefaultOnCallDate(i.Date, i.TimeStart);

        if (name.Length == 0) errors["patientName"] = ["Patient name is required."];
        else if (name.Length > 200) errors["patientName"] = ["Patient name is too long."];
        if (idNo.Length == 0) errors["patientIdNo"] = ["MyKad, KID, passport or registration number is required."];
        else if (idNo.Length > 40) errors["patientIdNo"] = ["The number is too long."];
        if (!Genders.Contains(gender)) errors["gender"] = ["Choose MALE or FEMALE."];
        if (department is not null && !Departments.Contains(department)) errors["department"] = ["Department is OMFS or PAEDS."];
        string? caseType = Upper(i.CaseType);
        if (caseType is not null && caseType is not ("TRAUMA" or "NON-TRAUMA")) errors["caseType"] = ["Case type is TRAUMA or NON-TRAUMA."];
        else if (strict && caseType is null) errors["caseType"] = ["Choose TRAUMA or NON-TRAUMA."];
        if (strict && department is null) errors["department"] = ["Choose the department (OMFS or PAEDS)."];
        string? referral = Upper(i.ReferralSource);
        string? edZone = referral == "ED" ? Upper(i.EdZone) : null;
        if (edZone is not null && edZone is not ("GREEN" or "YELLOW" or "RED")) errors["edZone"] = ["ED zone is GREEN, YELLOW or RED."];
        else if (strict && referral == "ED" && edZone is null) errors["edZone"] = ["Choose the ED zone."];
        if (onCall != i.Date && onCall != i.Date.AddDays(-1))
            errors["onCallDate"] = ["The on-call date is the case date, or the day before for a case after midnight."];
        foreach (var (key, value, max) in new[]
                 {
                     ("age", i.Age, 20), ("race", i.Race, 60), ("referralSource", i.ReferralSource, 60), ("diagnosis", i.Diagnosis, 500),
                     ("management", i.Management, 1000), ("plan", i.Plan, 1000), ("taggers", i.Taggers, 200),
                     ("dsaName", i.DsaName, 200), ("spokenTo", i.SpokenTo, 200), ("seenBy", i.SeenBy, 200),
                     ("presentation", i.Presentation, 500), ("investigations", i.Investigations, 300), ("prescription", i.Prescription, 500),
                 })
            if (value?.Trim().Length > max) errors[key] = [$"Too long (max {max} characters)."];
        if (errors.Count > 0) return errors;

        c.PersonId = officer;
        c.Date = i.Date;
        c.TimeStart = i.TimeStart;
        c.TimeFinish = i.TimeFinish;
        c.OnCallDate = onCall;
        c.PatientName = name;
        c.PatientIdNo = idNo;
        c.Gender = gender;
        c.Age = Upper(i.Age);
        c.Race = Upper(i.Race);
        c.ReferralSource = referral;
        c.EdZone = edZone;
        c.Department = department;
        c.Diagnosis = Upper(i.Diagnosis);
        c.Management = Upper(i.Management);
        c.Plan = Upper(i.Plan);
        c.SpokenTo = Upper(i.SpokenTo);
        c.SeenBy = Upper(i.SeenBy);
        c.CaseType = caseType;
        c.Presentation = Upper(i.Presentation);
        c.Investigations = Investigations(i.Investigations);
        c.Prescription = Upper(i.Prescription);
        c.Taggers = Upper(i.Taggers);
        c.DsaName = Upper(i.DsaName);
        c.DsaStatus = dsaStatus;
        c.Confidential = i.Confidential;
        return errors;
    }

    /// <summary>The shift a case belongs to: the case date, or the day before when it starts before 08:00.</summary>
    public static DateOnly DefaultOnCallDate(DateOnly date, TimeOnly start) => start < ShiftEnd ? date.AddDays(-1) : date;

    /// <summary>MyKad is stored without dashes or spaces ("900101-09-1234" -> "900101091234"); other numbers just trimmed and upper-cased.</summary>
    public static string NormalizeIdNo(string? s)
    {
        string t = (s ?? "").Trim().ToUpperInvariant();
        string digits = Regex.Replace(t, @"[\s-]", "");
        return digits.Length == 12 && digits.All(char.IsAsciiDigit) ? digits : Regex.Replace(t, @"\s+", " ");
    }

    /// <summary>"opg ,  fbc,,X-ray" -> "OPG, FBC, X-RAY".</summary>
    private static string? Investigations(string? s) =>
        Upper(string.Join(", ", (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : Regex.Replace(s.Trim(), @"[ \t]+", " ");
    private static string? Upper(string? s) => Clean(s)?.ToUpperInvariant();

    // ---------------- import from the Google Sheet ----------------

    private sealed class ImportRow
    {
        public int Line;
        public OnCallCase? Case;
        public string? Officer;
        public bool Duplicate;
        public List<ImportErrorDto> Errors = [];
    }

    /// <summary>Column -> how its header is recognised (Google Form question text, upper-cased).</summary>
    private static readonly (string Key, Func<string, bool> Match)[] Columns =
    [
        ("date", h => h == "DATE"),
        ("start", h => h.Contains("TIME START")),
        ("finish", h => h.Contains("TIME FINISH")),
        ("name", h => h.Contains("PATIENT NAME")),
        ("idNo", h => h.Contains("MYCARD") || h.Contains("REG NO")),
        ("gender", h => h == "GENDER"),
        ("age", h => h == "AGE"),
        ("race", h => h == "RACE"),
        ("referral", h => h.Contains("REFERRAL")),
        ("department", h => h == "DEPARTMENT"),
        ("diagnosis", h => h == "DIAGNOSIS"),
        ("management", h => h == "MANAGEMENT"),
        ("plan", h => h == "PLAN"),
        ("officer", h => h.Contains("OFFICER")),
        ("dsa", h => h.Contains("DSA")),
    ];

    private static readonly string[] DateFormats = ["d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "yyyy-MM-dd", "d-M-yyyy", "d.M.yyyy"];
    private static readonly string[] TimeFormats = ["H:mm", "H:mm:ss", "h:mm tt", "h:mm:ss tt", "hh:mm tt", "hh:mm:ss tt", "HHmm"];

    private static async Task<(List<ImportRow> Rows, List<string> FileErrors, int Duplicates)> CheckImportAsync(RotaDbContext db,
        CaseImportRequest req, Guid importer, CancellationToken ct)
    {
        var lines = Csv.Read(req.Csv ?? "");
        var fileErrors = new List<string>();
        if (lines.Count == 0) return ([], ["The file is empty."], 0);

        var headers = lines[0].Fields.Select(h => Regex.Replace(h.Trim().ToUpperInvariant(), @"\s+", " ")).ToArray();
        var col = new Dictionary<string, int>();
        foreach (var (key, match) in Columns)
            if (Array.FindIndex(headers, h => match(h)) is var i and >= 0) col[key] = i;
        var missing = new[] { "date", "start", "finish", "name", "idNo", "gender" }.Where(k => !col.ContainsKey(k)).ToList();
        if (missing.Count > 0) fileErrors.Add($"Missing column(s): {string.Join(", ", missing)}. Export the form's response sheet as CSV.");
        if (lines.Count - 1 > MaxImportRows) fileErrors.Add($"At most {MaxImportRows} rows at a time.");
        if (fileErrors.Count > 0) return ([], fileErrors, 0);

        // Officers (also those who left) by their name without "Dr", longest first so "Ali bin Abu" wins over "Ali".
        var officers = (await db.Officers.AsNoTracking().Select(p => new { p.Id, p.Name }).ToListAsync(ct))
            .Select(p => (p.Id, p.Name, Key: OfficerKey(p.Name))).Where(p => p.Key.Length >= 3).OrderByDescending(p => p.Key.Length).ToList();
        string? defaultName = req.DefaultPersonId is { } d ? officers.FirstOrDefault(o => o.Id == d).Name : null;
        if (req.DefaultPersonId is not null && defaultName is null) fileErrors.Add("The officer chosen for unmatched rows isn't on the rota.");

        var existing = (await db.Cases.AsNoTracking().Select(c => new { c.PersonId, c.Date, c.TimeStart, c.PatientIdNo }).ToListAsync(ct))
            .Select(c => (c.PersonId, c.Date, c.TimeStart, c.PatientIdNo)).ToHashSet();
        var rows = new List<ImportRow>();
        int duplicates = 0;

        foreach (var (line, fields) in lines.Skip(1))
        {
            string? F(string key) => col.TryGetValue(key, out var i) && i < fields.Length && fields[i].Trim().Length > 0 ? fields[i].Trim() : null;
            if (fields.All(f => f.Trim().Length == 0)) continue;
            var row = new ImportRow { Line = line };
            void Error(string key, string message) =>
                row.Errors.Add(new ImportErrorDto(headers[col[key]], AdminImportEndpoints.ColumnLetter(col[key]), message));

            bool dateOk = DateOnly.TryParseExact(F("date"), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
            bool startOk = TimeOnly.TryParseExact(F("start"), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start);
            bool finishOk = TimeOnly.TryParseExact(F("finish"), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var finish);
            if (!dateOk) Error("date", $"\"{F("date")}\" is not a date (day/month/year).");
            if (!startOk) Error("start", $"\"{F("start")}\" is not a time.");
            if (!finishOk) Error("finish", $"\"{F("finish")}\" is not a time.");

            string officerText = F("officer") ?? "";
            string key = OfficerKey(officerText);
            var match = officers.Where(o => key.Contains(o.Key)).OrderBy(o => key.IndexOf(o.Key, StringComparison.Ordinal)).FirstOrDefault();
            Guid? personId = match.Key is not null ? match.Id : req.DefaultPersonId;
            row.Officer = match.Key is not null ? match.Name : defaultName;
            if (personId is null)
                row.Errors.Add(new ImportErrorDto(col.ContainsKey("officer") ? headers[col["officer"]] : "Officer",
                    col.TryGetValue("officer", out var oc) ? AdminImportEndpoints.ColumnLetter(oc) : "",
                    $"No officer on the rota matches \"{officerText}\". Pick an officer for unmatched rows."));

            if (dateOk && startOk && finishOk)
            {
                // The old form had one "officer & taggers" column (kept as the taggers text) and one DSA column holding either
                // a status or, under "Other", the DSA's name.
                string? dsa = F("dsa")?.ToUpperInvariant();
                bool dsaIsStatus = dsa is not null && DsaStatuses.Contains(dsa);
                var c = new OnCallCase { RecordedById = importer };
                var errors = Apply(c, new CaseInput(date, start, finish, null, F("name") ?? "", F("idNo") ?? "", F("gender") ?? "",
                    F("age"), F("race"), F("referral"), F("department"), F("diagnosis"), F("management"), F("plan"), null,
                    officerText.Length > 0 ? officerText : null, dsaIsStatus ? null : dsa, dsaIsStatus ? dsa : null, false),
                    personId ?? Guid.Empty, strict: false);
                var keyOf = new Dictionary<string, string>
                {
                    ["patientName"] = "name", ["patientIdNo"] = "idNo", ["gender"] = "gender", ["department"] = "department",
                    ["age"] = "age", ["race"] = "race", ["referralSource"] = "referral", ["diagnosis"] = "diagnosis",
                    ["management"] = "management", ["plan"] = "plan", ["taggers"] = "officer", ["dsaName"] = "dsa", ["dsaStatus"] = "dsa",
                };
                foreach (var (field, messages) in errors)
                    if (keyOf.TryGetValue(field, out var k) && col.ContainsKey(k)) Error(k, messages[0]);
                    else row.Errors.Add(new ImportErrorDto(field, "", messages[0]));
                if (errors.Count == 0)
                {
                    row.Case = c;
                    row.Duplicate = !existing.Add((c.PersonId, c.Date, c.TimeStart, c.PatientIdNo));
                    if (row.Duplicate) duplicates++;
                }
            }
            rows.Add(row);
        }
        return (rows, fileErrors, duplicates);
    }

    /// <summary>"Dr. Aminah binti Ali" -> "AMINAH BINTI ALI", for finding officers in the free-text "officer & taggers" column.</summary>
    private static string OfficerKey(string s) =>
        DrPrefix().Replace(Regex.Replace(s.ToUpperInvariant(), @"[^A-Z0-9@/ ]+", " "), " ").Trim() is var t ? Regex.Replace(t, @"\s+", " ") : "";

    [GeneratedRegex(@"\bDR\b")]
    private static partial Regex DrPrefix();
}

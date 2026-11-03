using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;
using static Rota.Api.Endpoints.AdminEndpoints;

namespace Rota.Api.Endpoints;

/// <summary>Adds many officers from a CSV file, each placed in a clinic and optionally invited by email.
/// The file is checked first; nothing is saved unless every row is valid.</summary>
public static class AdminImportEndpoints
{
    public const int MaxRows = 1000;

    /// <summary>Column headers (case-insensitive) and the Malay names also accepted.</summary>
    private static readonly Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "name", ["nama"] = "name",
        ["code"] = "code", ["kod"] = "code",
        ["email"] = "email", ["emel"] = "email",
        ["role"] = "role", ["peranan"] = "role",
        ["phone"] = "phone", ["telefon"] = "phone", ["no telefon"] = "phone",
        ["state"] = "state", ["negeri"] = "state",
        ["district"] = "district", ["daerah"] = "district",
        ["clinic"] = "clinic", ["klinik"] = "clinic",
    };

    private static readonly string[] Required = ["name", "state", "district", "clinic"];

    /// <summary>The headers of the downloadable template, in order.</summary>
    private static readonly string[] Template = ["Name", "Code", "Email", "Role", "Phone", "State", "District", "Clinic"];

    public static void MapImportEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapPost("/people/import", async (ImportPeopleRequest req, RotaDbContext db, Mailer mailer, HttpContext http,
            CancellationToken ct) =>
        {
            var (rows, fileErrors) = await CheckAsync(db, req.Csv ?? "", ct);
            if (!req.Commit || fileErrors.Count > 0 || rows.Count == 0 || rows.Any(r => r.Errors.Count > 0))
                return Results.Ok(new ImportResultDto(rows.Select(r => r.ToDto()).ToList(), fileErrors, false, 0, 0, []));

            var codes = await NextCodeAsync(db, rows.Count(r => r.Code is null), ct, rows.Select(r => r.Code).OfType<string>());
            int order = await NextSortOrderAsync(db, ct), next = 0, invited = 0;
            var passwords = new List<IssuedPasswordDto>();

            foreach (var (row, i) in rows.Select((r, i) => (r, i)))
            {
                var person = new Person
                {
                    Name = row.Name, Code = row.Code ?? codes[next++], ClinicId = row.ClinicId, Phone = row.Phone, SortOrder = order + i,
                };
                db.People.Add(person);
                if (row.Email is null) continue;

                var invite = AdminAccountEndpoints.Invite(db, mailer, http, row.Email, row.Role, person.Id);
                invited++;
                if (invite.TempPassword is { } temp) passwords.Add(new IssuedPasswordDto(row.Name, row.Email, temp));
            }

            Mapping.Log(db, http, "person.import", null, new
            {
                added = rows.Select(r => new { r.Name, r.Code, r.Email, r.Clinic }).ToList(), invited,
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ImportResultDto(rows.Select(r => r.ToDto()).ToList(), [], true, rows.Count, invited, passwords));
        });
    }

    private sealed class Row
    {
        public int Line;
        public string Name = "";
        public string? Code, Email, Phone, State, District, Clinic;
        public AccountRole Role = AccountRole.Officer;
        public string? RoleText;
        public Guid? ClinicId;
        public List<ImportErrorDto> Errors = [];

        public ImportRowDto ToDto() => new(Line, Name, Code, Email, Email is null ? null : Role, Phone, State, District, Clinic, Errors);
    }

    /// <summary>Spreadsheet column letter for a 0-based index: 0 = A, 25 = Z, 26 = AA.</summary>
    public static string ColumnLetter(int index)
    {
        var letters = "";
        for (int n = index + 1; n > 0; n = (n - 1) / 26) letters = (char)('A' + (n - 1) % 26) + letters;
        return letters;
    }

    private static async Task<(List<Row> Rows, List<string> FileErrors)> CheckAsync(RotaDbContext db, string csv, CancellationToken ct)
    {
        var lines = Csv.Read(csv);
        if (lines.Count == 0) return ([], ["The file is empty."]);

        // Header -> column index; unknown columns are ignored.
        var header = lines[0].Fields;
        var columns = new Dictionary<string, int>();
        foreach (var (name, i) in header.Select((h, i) => (h, i)))
            if (Headers.TryGetValue(name.Trim(), out var key)) columns.TryAdd(key, i);
        var missing = Required.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            var found = header.Where(h => h.Length > 0).ToList();
            return ([], [$"Missing column(s): {string.Join(", ", missing)}. The first row should be the headers "
                + $"{string.Join(", ", Template)}"
                + (found.Count > 0 ? $", but it is: {string.Join(", ", found)}." : ", but it is blank.")
                + " Download the template for the expected headers."]);
        }
        if (lines.Count - 1 > MaxRows) return ([], [$"Too many officers ({lines.Count - 1}, max {MaxRows}). Split the file."]);

        string? Field(string[] fields, string key) =>
            columns.TryGetValue(key, out int i) && i < fields.Length ? Clean(fields[i]) : null;

        // Every problem names the cell: the column's header as written in the file and its letter.
        void Error(Row r, string key, string message)
        {
            int i = columns.TryGetValue(key, out int c) ? c : 0;
            r.Errors.Add(new ImportErrorDto(header[i], ColumnLetter(i), message));
        }

        var rows = lines.Skip(1).Select(l => new Row
        {
            Line = l.Line, Name = Field(l.Fields, "name") ?? "", Code = Field(l.Fields, "code"),
            Email = Field(l.Fields, "email") is { } e ? AuthEndpoints.NormalizeEmail(e) : null, RoleText = Field(l.Fields, "role"),
            Phone = Field(l.Fields, "phone"), State = Field(l.Fields, "state"), District = Field(l.Fields, "district"),
            Clinic = Field(l.Fields, "clinic"),
        }).ToList();
        if (rows.Count == 0) return ([], ["The file has a header but no officers."]);

        var names = (await db.People.Select(p => p.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codes = (await db.People.Select(p => p.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = (await db.Accounts.Select(a => a.Email).ToListAsync(ct)).ToHashSet();
        var clinics = await db.Clinics.Include(c => c.District).ThenInclude(d => d!.State).ToListAsync(ct);
        var states = await db.States.Select(s => s.Name).OrderBy(n => n).ToListAsync(ct);
        var districts = await db.Districts.Include(d => d.State).ToListAsync(ct);

        // First row each value appears on, to flag repeats within the file.
        var seenNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new Dictionary<string, int>();

        foreach (var r in rows)
        {
            if (r.Name.Length == 0) Error(r, "name", "Name is required.");
            else if (r.Name.Length > 200) Error(r, "name", "Name is too long (max 200 characters).");
            else if (names.Contains(r.Name)) Error(r, "name", $"{r.Name} is already in the officer list.");
            else if (!seenNames.TryAdd(r.Name, r.Line)) Error(r, "name", $"Same name as row {seenNames[r.Name]}.");

            if (r.Code is { } code)
            {
                if (code.Length > 32) Error(r, "code", "Code is too long (max 32 characters).");
                else if (codes.Contains(code)) Error(r, "code", $"Code {code} is already used by another officer.");
                else if (!seenCodes.TryAdd(code, r.Line)) Error(r, "code", $"Same code as row {seenCodes[code]}.");
            }

            if (r.Email is { } email)
            {
                if (!AdminAccountEndpoints.IsValidEmail(email)) Error(r, "email", $"{email} is not a valid email address.");
                else if (emails.Contains(email)) Error(r, "email", $"{email} already has an account.");
                else if (!seenEmails.TryAdd(email, r.Line)) Error(r, "email", $"Same email as row {seenEmails[email]}.");

                if (r.RoleText is null || r.RoleText.Equals("officer", StringComparison.OrdinalIgnoreCase)) r.Role = AccountRole.Officer;
                else if (r.RoleText.Equals("admin", StringComparison.OrdinalIgnoreCase)) r.Role = AccountRole.Admin;
                else Error(r, "role", $"Role must be Officer or Admin, not {r.RoleText}.");
            }

            if (r.Phone?.Length > 32) Error(r, "phone", "Phone number is too long (max 32 characters).");

            CheckPlace(r, Error, states, districts, clinics);
        }

        return (rows, []);
    }

    /// <summary>State, district and clinic must exist and fit together; sets the row's clinic.</summary>
    private static void CheckPlace(Row r, Action<Row, string, string> error, List<string> states, List<District> districts,
        List<Clinic> clinics)
    {
        static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        static string Names(IEnumerable<string> names) => names.Any() ? string.Join(", ", names) : "none yet";

        if (r.State is null) error(r, "state", "State is required.");
        if (r.District is null) error(r, "district", "District is required.");
        if (r.Clinic is null) error(r, "clinic", "Clinic is required.");
        if (r.State is null || r.District is null || r.Clinic is null) return;

        if (!states.Any(s => Same(s, r.State)))
        {
            error(r, "state", $"{r.State} is not set up as a state. Add it under Clinics & districts first, "
                + $"or fix the spelling. States set up: {Names(states)}.");
            return;
        }
        if (!districts.Any(d => Same(d.Name, r.District) && Same(d.State!.Name, r.State)))
        {
            var inState = districts.Where(d => Same(d.State!.Name, r.State)).Select(d => d.Name).Order();
            error(r, "district", $"{r.District} is not set up as a district of {r.State}. Add it under Clinics & districts first, "
                + $"or fix the spelling. Districts of {r.State}: {Names(inState)}.");
            return;
        }

        var clinic = clinics.FirstOrDefault(c => Same(c.Name, r.Clinic));
        if (clinic is null)
        {
            var inDistrict = clinics.Where(c => Same(c.District!.Name, r.District) && Same(c.District.State!.Name, r.State))
                .Select(c => c.Name).Order();
            error(r, "clinic", $"{r.Clinic} is not set up as a clinic. Add it under Clinics & districts first, "
                + $"or fix the spelling. Clinics in {r.District}: {Names(inDistrict)}.");
        }
        else if (!Same(clinic.District!.Name, r.District) || !Same(clinic.District.State!.Name, r.State))
            error(r, Same(clinic.District.State!.Name, r.State) ? "district" : "state",
                $"{clinic.Name} is in {clinic.District.Name}, {clinic.District.State!.Name}, not {r.District}, {r.State}.");
        else
        {
            r.ClinicId = clinic.Id;
            // Use the names as stored, so the preview shows the canonical spelling.
            (r.Clinic, r.District, r.State) = (clinic.Name, clinic.District.Name, clinic.District.State!.Name);
        }
    }
}

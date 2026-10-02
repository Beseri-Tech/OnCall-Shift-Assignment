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
        public List<string> Errors = [];

        public ImportRowDto ToDto() => new(Line, Name, Code, Email, Email is null ? null : Role, Phone, State, District, Clinic, Errors);
    }

    private static async Task<(List<Row> Rows, List<string> FileErrors)> CheckAsync(RotaDbContext db, string csv, CancellationToken ct)
    {
        var lines = Csv.Read(csv);
        if (lines.Count == 0) return ([], ["The file is empty."]);

        // Header -> column index; unknown columns are ignored.
        var columns = new Dictionary<string, int>();
        foreach (var (header, i) in lines[0].Fields.Select((h, i) => (h, i)))
            if (Headers.TryGetValue(header.Trim(), out var key)) columns.TryAdd(key, i);
        var missing = Required.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            return ([], [$"Missing column(s): {string.Join(", ", missing)}. Download the template for the expected headers."]);
        if (lines.Count - 1 > MaxRows) return ([], [$"Too many rows (max {MaxRows}). Split the file."]);

        string? Field(string[] fields, string key) =>
            columns.TryGetValue(key, out int i) && i < fields.Length ? Clean(fields[i]) : null;

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
        var states = await db.States.Select(s => s.Name).ToListAsync(ct);
        var districts = await db.Districts.Include(d => d.State).ToListAsync(ct);

        // First line each value appears on, to flag repeats within the file.
        var seenNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new Dictionary<string, int>();

        foreach (var r in rows)
        {
            if (r.Name.Length == 0) r.Errors.Add("Name is required.");
            else if (r.Name.Length > 200) r.Errors.Add("Name is too long (max 200).");
            else if (names.Contains(r.Name)) r.Errors.Add($"{r.Name} is already in the list.");
            else if (!seenNames.TryAdd(r.Name, r.Line)) r.Errors.Add($"Same name as line {seenNames[r.Name]}.");

            if (r.Code is { } code)
            {
                if (code.Length > 32) r.Errors.Add("Code is too long (max 32).");
                else if (codes.Contains(code)) r.Errors.Add($"Code {code} is already used.");
                else if (!seenCodes.TryAdd(code, r.Line)) r.Errors.Add($"Same code as line {seenCodes[code]}.");
            }

            if (r.Email is { } email)
            {
                if (!AdminAccountEndpoints.IsValidEmail(email)) r.Errors.Add($"{email} is not a valid email address.");
                else if (emails.Contains(email)) r.Errors.Add($"{email} already has an account.");
                else if (!seenEmails.TryAdd(email, r.Line)) r.Errors.Add($"Same email as line {seenEmails[email]}.");

                if (r.RoleText is null || r.RoleText.Equals("officer", StringComparison.OrdinalIgnoreCase)) r.Role = AccountRole.Officer;
                else if (r.RoleText.Equals("admin", StringComparison.OrdinalIgnoreCase)) r.Role = AccountRole.Admin;
                else r.Errors.Add($"Role must be Officer or Admin, not {r.RoleText}.");
            }

            if (r.Phone?.Length > 32) r.Errors.Add("Phone number is too long (max 32).");

            CheckPlace(r, states, districts, clinics);
        }

        return (rows, []);
    }

    /// <summary>State, district and clinic must exist and fit together; sets the row's clinic.</summary>
    private static void CheckPlace(Row r, List<string> states, List<District> districts, List<Clinic> clinics)
    {
        static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        if (r.State is null || r.District is null || r.Clinic is null)
        {
            r.Errors.Add("State, district and clinic are required.");
            return;
        }
        if (!states.Any(s => Same(s, r.State)))
        {
            r.Errors.Add($"Unknown state {r.State}.");
            return;
        }
        if (!districts.Any(d => Same(d.Name, r.District) && Same(d.State!.Name, r.State)))
        {
            r.Errors.Add($"Unknown district {r.District} in {r.State}.");
            return;
        }

        var clinic = clinics.FirstOrDefault(c => Same(c.Name, r.Clinic));
        if (clinic is null) r.Errors.Add($"Unknown clinic {r.Clinic}. Add it under Clinics first.");
        else if (!Same(clinic.District!.Name, r.District) || !Same(clinic.District.State!.Name, r.State))
            r.Errors.Add($"{clinic.Name} is in {clinic.District.Name}, {clinic.District.State!.Name}, not {r.District}, {r.State}.");
        else
        {
            r.ClinicId = clinic.Id;
            // Use the names as stored, so the preview shows the canonical spelling.
            (r.Clinic, r.District, r.State) = (clinic.Name, clinic.District.Name, clinic.District.State!.Name);
        }
    }
}

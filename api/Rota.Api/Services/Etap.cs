using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Endpoints;

namespace Rota.Api.Services;

/// <summary>
/// Builds an officer's ETAP claim for a month from what they typed (dates and times), the rota and their cases.
/// Passive call only (dental officers are on call from home): a shift whose cases add up to 4 hours or more is
/// "melebihi 4 jam" (category 2), otherwise category 3; (a) on working days, (b) on rest days and public holidays.
/// </summary>
public static class Etap
{
    /// <summary>A case total of at least this many minutes on one on-call date is a "passive call melebihi 4 jam".</summary>
    public const int LongCallMinutes = 4 * 60;

    /// <summary>The form's 30 rows in section B.</summary>
    public const int MaxRows = 30;

    // ponytail: rates of the 30/10/2025 circular as constants; move to config when a new circular changes them.
    private static readonly Dictionary<string, (int Officer, int Specialist)> Rates = new()
    {
        ["2a"] = (180, 200), ["2b"] = (190, 220), ["3a"] = (110, 135), ["3b"] = (120, 150),
    };

    public static readonly (string Code, string Label)[] Categories =
    [
        ("2a", "Passive call melebihi 4 jam: hari bekerja"),
        ("2b", "Passive call melebihi 4 jam: hari rehat / kelepasan"),
        ("3a", "Passive call kurang 4 jam: hari bekerja"),
        ("3b", "Passive call kurang 4 jam: hari rehat / kelepasan"),
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<EtapRowInput> ReadRows(string json) => JsonSerializer.Deserialize<List<EtapRowInput>>(json, Json) ?? [];
    public static string WriteRows(IEnumerable<EtapRowInput> rows) => JsonSerializer.Serialize(rows.OrderBy(r => r.Date), Json);

    /// <summary>The usual times for an on-call date: 17:00 to 08:00 next day on working days, 08:00 to 08:00 (24 h) otherwise.</summary>
    public static EtapRowInput DefaultRow(DateOnly date, EtapDayType type) => type == EtapDayType.Working
        ? new(date, date, new TimeOnly(17, 0), date.AddDays(1), new TimeOnly(8, 0))
        : new(date, date, new TimeOnly(8, 0), date.AddDays(1), new TimeOnly(8, 0));

    public static int Minutes(EtapRowInput r) =>
        (int)(r.ToDate.ToDateTime(r.ToTime) - r.FromDate.ToDateTime(r.FromTime)).TotalMinutes;

    /// <summary>Problems with the typed rows (by row), or empty.</summary>
    public static Dictionary<string, string[]> Validate(int year, int month, IReadOnlyList<EtapRowInput> rows)
    {
        var errors = new Dictionary<string, string[]>();
        if (rows.Count > MaxRows) errors["rows"] = [$"The form has room for {MaxRows} on-call days."];
        foreach (var dup in rows.GroupBy(r => r.Date).Where(g => g.Count() > 1))
            errors[$"rows.{dup.Key:yyyy-MM-dd}"] = [$"{dup.Key:d MMM} is listed twice."];
        foreach (var r in rows)
        {
            string key = $"rows.{r.Date:yyyy-MM-dd}";
            if (r.Date.Year != year || r.Date.Month != month) errors[key] = [$"{r.Date:d MMM yyyy} is not in this month."];
            else if (Minutes(r) is <= 0 or > 48 * 60) errors[key] = [$"{r.Date:d MMM}: the end must be after the start (at most 48 hours)."];
            else if (r.FromDate < r.Date || r.FromDate > r.Date.AddDays(1)) errors[key] = [$"{r.Date:d MMM}: the start must be on the on-call date."];
        }
        return errors;
    }

    /// <summary>Everything the ETAP page and PDF need for one officer and month. Rows null = their saved draft.</summary>
    public static async Task<EtapDto> BuildAsync(RotaDbContext db, Person me, int year, int month, IReadOnlyList<EtapRowInput>? rows,
        CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var end = first.AddMonths(1);
        var claim = await db.EtapClaims.AsNoTracking().FirstOrDefaultAsync(c => c.PersonId == me.Id && c.Year == year && c.Month == month, ct);
        rows ??= claim is null ? [] : ReadRows(claim.Rows);

        var weekend = await db.Facilities.Where(f => f.Id == me.FacilityId).Select(f => f.District!.State!.WeekendDays).FirstOrDefaultAsync(ct);
        var holidays = (await db.Holidays.Where(h => h.Date >= first && h.Date < end).Select(h => new { h.Date, h.Name }).ToListAsync(ct))
            .Concat(await db.PeriodDays.Where(d => d.Kind == PeriodDayKind.Holiday && d.Date >= first && d.Date < end)
                .Select(d => new { d.Date, d.Name }).ToListAsync(ct))
            .GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.First().Name);
        EtapDayType TypeOf(DateOnly d) => holidays.ContainsKey(d) ? EtapDayType.Holiday
            : (weekend & (1 << (int)d.DayOfWeek)) != 0 ? EtapDayType.Rest : EtapDayType.Working;

        var cases = await db.Cases.AsNoTracking().Where(c => c.PersonId == me.Id && c.OnCallDate >= first && c.OnCallDate < end)
            .OrderBy(c => c.Date).ThenBy(c => c.TimeStart).ToListAsync(ct);
        var rotaDates = await (from a in db.Assignments
                               join r in db.Runs on a.RunId equals r.Id
                               where a.PersonId == me.Id && r.IsPublished && a.Date >= first && a.Date < end
                               select a.Date).Distinct().OrderBy(d => d).ToListAsync(ct);
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == me.Id, ct);
        bool specialist = profile?.TarikhPewartaanPakar is not null;

        var rowDtos = new List<EtapRowDto>();
        foreach (var r in rows.OrderBy(r => r.Date))
        {
            var type = TypeOf(r.Date);
            var mine = cases.Where(c => c.OnCallDate == r.Date).ToList();
            int caseMinutes = mine.Sum(c => c.Minutes);
            string category = (caseMinutes >= LongCallMinutes ? "2" : "3") + (type == EtapDayType.Working ? "a" : "b");
            bool inRota = rotaDates.Contains(r.Date);
            var rotaOfficer = inRota ? null : await CaseEndpoints.OnCallOfficerAsync(db, r.Date, ct);
            rowDtos.Add(new EtapRowDto(r.Date, r.FromDate, r.FromTime, r.ToDate, r.ToTime, Minutes(r), type, holidays.GetValueOrDefault(r.Date),
                caseMinutes, mine.Count, category, inRota, rotaOfficer));
        }

        var lines = Categories.Select(c =>
        {
            int days = rowDtos.Count(r => r.Category == c.Code);
            int rate = specialist ? Rates[c.Code].Specialist : Rates[c.Code].Officer;
            return new EtapLineDto(c.Code, c.Label, days, rate, days * rate);
        }).ToList();

        var longDates = rowDtos.Where(r => r.Category.StartsWith('2')).Select(r => r.Date).ToHashSet();
        var patients = cases.Where(c => longDates.Contains(c.OnCallDate)).Select(c => new EtapPatientDto(c.Date,
            c.Confidential ? null : c.PatientName, c.Confidential ? null : c.PatientIdNo, c.Minutes,
            string.Join(" / ", new[] { c.Diagnosis, c.Management }.Where(s => !string.IsNullOrWhiteSpace(s))), c.Department,
            c.Confidential)).ToList();

        var letter = await (from s in db.SuratArahan
                            join d in db.Documents on s.DocumentId equals d.Id
                            where s.EffectiveFrom < end && d.Status == DocumentStatus.Available
                            orderby s.EffectiveFrom descending, s.CreatedAt descending
                            select new SuratArahanDto(s.Id, s.DocumentId, d.FileName, s.EffectiveFrom, s.Reference, s.CreatedAt))
            .FirstOrDefaultAsync(ct);

        bool verified = profile is not null && ProfileEndpoints.IsVerified(me, profile);
        return new EtapDto($"{year:0000}-{month:00}", verified, specialist, rowDtos, rotaDates, lines, lines.Sum(l => l.Days),
            lines.Sum(l => l.Amount), patients, letter, claim is not null,
            rotaDates.Where(d => rows.All(r => r.Date != d)).Select(d => DefaultRow(d, TypeOf(d))).ToList());
    }
}

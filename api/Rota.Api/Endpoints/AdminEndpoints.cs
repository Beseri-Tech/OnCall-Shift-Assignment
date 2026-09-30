using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Leave;

namespace Rota.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Session.AdminPolicy);
        MapPeople(admin.MapGroup("/people").WithTags("Admin people"));
        MapClinics(admin.MapGroup("/clinics").WithTags("Admin clinics"));
        MapPeriods(admin.MapGroup("/periods").WithTags("Admin periods"));
        MapHolidays(admin.MapGroup("/holidays").WithTags("Admin holidays"));
        MapPeakDays(admin.MapGroup("/peak-days").WithTags("Admin peak days"));
        admin.MapRotaEndpoints();
        admin.MapTallyEndpoints();
        admin.MapAccountEndpoints();
    }

    // ---------------- people ----------------

    private static void MapPeople(RouteGroupBuilder g)
    {
        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) => await AdminPeopleAsync(db, ct));

        g.MapPost("/", async (UpsertPersonRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var person = new Person { SortOrder = await NextSortOrderAsync(db, ct) };
            if (await ValidateAsync(db, req, null, ct) is { } problem) return problem;

            Apply(person, req);
            person.Code = string.IsNullOrWhiteSpace(req.Code)
                ? (await NextCodeAsync(db, 1, ct))[0]
                : req.Code.Trim();
            db.People.Add(person);
            Mapping.Log(db, http, "person.create", person.Id, new { person.Name, person.Code });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/people/{person.Id}", person.Id);
        });

        g.MapPost("/bulk", async (BulkAddRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var existing = (await db.People.Select(p => p.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var names = req.Names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => Regex.Replace(n, @"\s+", " "))
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var toAdd = names.Where(n => !existing.Contains(n)).ToList();
            var codes = await NextCodeAsync(db, toAdd.Count, ct);
            int order = await NextSortOrderAsync(db, ct);

            for (int i = 0; i < toAdd.Count; i++)
                db.People.Add(new Person { Name = toAdd[i], Code = codes[i], SortOrder = order + i });

            Mapping.Log(db, http, "person.bulk-add", null, new { added = toAdd });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { added = toAdd, skipped = names.Except(toAdd).ToList() });
        });

        g.MapPut("/{id:guid}", async (Guid id, UpsertPersonRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var person = await db.People.FindAsync([id], ct);
            if (person is null) return Results.NotFound();
            if (await ValidateAsync(db, req, id, ct) is { } problem) return problem;

            Apply(person, req);
            if (!string.IsNullOrWhiteSpace(req.Code)) person.Code = req.Code.Trim();
            person.UpdatedAt = DateTimeOffset.UtcNow;
            Mapping.Log(db, http, "person.update", id, req);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/reorder", async (ReorderRequest req, RotaDbContext db, CancellationToken ct) =>
        {
            var people = await db.People.ToDictionaryAsync(p => p.Id, ct);
            for (int i = 0; i < req.Ids.Count; i++)
                if (people.TryGetValue(req.Ids[i], out var p)) p.SortOrder = i;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var person = await db.People.FindAsync([id], ct);
            if (person is null) return Results.NotFound();
            if (await db.Assignments.AnyAsync(a => a.PersonId == id, ct))
                return Results.Problem("This person has shifts in a rota. Mark them as Left instead so their history is kept.",
                    statusCode: StatusCodes.Status409Conflict);

            db.People.Remove(person);
            Mapping.Log(db, http, "person.delete", id, new { person.Name, person.Code });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    internal static async Task<List<AdminPersonDto>> AdminPeopleAsync(RotaDbContext db, CancellationToken ct)
    {
        var people = await db.People.Include(p => p.Clinic).OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        return people.Select(p => new AdminPersonDto(p.Id, p.Code, p.Name, p.Status, p.StatusReason, p.ExcludedUntil,
            p.ClinicId, p.Clinic?.Name, p.Clinic?.Area, p.Phone, p.SortOrder, p.ExtraShift, p.PreferWeekendHoliday,
            p.WeekendWeight)).ToList();
    }

    private static void Apply(Person p, UpsertPersonRequest req)
    {
        p.Name = Regex.Replace(req.Name.Trim(), @"\s+", " ");
        p.Status = req.Status;
        p.StatusReason = req.Status == OfficerStatus.OnCall ? null : Clean(req.StatusReason);
        p.ExcludedUntil = req.Status == OfficerStatus.Excluded ? req.ExcludedUntil : null;
        p.ClinicId = req.ClinicId;
        p.Phone = Clean(req.Phone);
        p.ExtraShift = req.ExtraShift;
        p.PreferWeekendHoliday = req.PreferWeekendHoliday;
        p.WeekendWeight = req.WeekendWeight;
    }

    private static async Task<IResult?> ValidateAsync(RotaDbContext db, UpsertPersonRequest req, Guid? id, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string name = Regex.Replace(req.Name?.Trim() ?? "", @"\s+", " ");

        if (name.Length == 0) errors["name"] = ["Name is required."];
        else if (await db.People.AnyAsync(p => p.Id != id && p.Name.ToLower() == name.ToLower(), ct))
            errors["name"] = [$"{name} is already in the list."];

        if (!string.IsNullOrWhiteSpace(req.Code) &&
            await db.People.AnyAsync(p => p.Id != id && p.Code == req.Code.Trim(), ct))
            errors["code"] = [$"Code {req.Code.Trim()} is already used."];

        if (!Enum.IsDefined(req.Status)) errors["status"] = ["Unknown status."];
        else if (req.Status != OfficerStatus.OnCall && Clean(req.StatusReason) is null)
            errors["statusReason"] = ["Give a reason (e.g. CUTI BERSALIN, PINDAH)."];
        if (Clean(req.StatusReason)?.Length > 200) errors["statusReason"] = ["Reason is too long (max 200)."];
        if (Clean(req.Phone)?.Length > 32) errors["phone"] = ["Phone number is too long."];
        if (req.ClinicId is { } clinicId && !await db.Clinics.AnyAsync(c => c.Id == clinicId, ct))
            errors["clinicId"] = ["Unknown clinic."];

        if (req.WeekendWeight is < 1 or > 5) errors["weekendWeight"] = ["Weekend weight must be 1-5."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : Regex.Replace(s.Trim(), @"\s+", " ");

    private static async Task<int> NextSortOrderAsync(RotaDbContext db, CancellationToken ct) =>
        (await db.People.MaxAsync(p => (int?)p.SortOrder, ct) ?? -1) + 1;

    /// <summary>Next free codes D001, D002, ... after the highest existing D-number.</summary>
    internal static async Task<List<string>> NextCodeAsync(RotaDbContext db, int count, CancellationToken ct)
    {
        var codes = await db.People.Select(p => p.Code).ToListAsync(ct);
        int max = codes.Select(c => Regex.Match(c, @"^D(\d+)$")).Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return Enumerable.Range(max + 1, count).Select(n => $"D{n:000}").ToList();
    }

    // ---------------- clinics ----------------

    private static void MapClinics(RouteGroupBuilder g)
    {
        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
            await db.Clinics.OrderBy(c => c.Area).ThenBy(c => c.Name)
                .Select(c => new ClinicDto(c.Id, c.Name, c.Area, db.People.Count(p => p.ClinicId == c.Id)))
                .ToListAsync(ct));

        g.MapPost("/", async (UpsertClinicRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await ValidateClinicAsync(db, req, null, ct) is { } problem) return problem;
            var clinic = new Clinic { Name = Clean(req.Name)!, Area = Clean(req.Area)! };
            db.Clinics.Add(clinic);
            Mapping.Log(db, http, "clinic.create", null, req);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/clinics/{clinic.Id}", clinic.Id);
        });

        g.MapPut("/{id:guid}", async (Guid id, UpsertClinicRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var clinic = await db.Clinics.FindAsync([id], ct);
            if (clinic is null) return Results.NotFound();
            if (await ValidateClinicAsync(db, req, id, ct) is { } problem) return problem;
            clinic.Name = Clean(req.Name)!;
            clinic.Area = Clean(req.Area)!;
            Mapping.Log(db, http, "clinic.update", null, new { id, req.Name, req.Area });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Officers of a deleted clinic just lose their clinic (FK is ON DELETE SET NULL).
        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var clinic = await db.Clinics.FindAsync([id], ct);
            if (clinic is null) return Results.NotFound();
            db.Clinics.Remove(clinic);
            Mapping.Log(db, http, "clinic.delete", null, new { id, clinic.Name });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult?> ValidateClinicAsync(RotaDbContext db, UpsertClinicRequest req, Guid? id, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string? name = Clean(req.Name), area = Clean(req.Area);

        if (name is null) errors["name"] = ["Name is required."];
        else if (name.Length > 100) errors["name"] = ["Name is too long (max 100)."];
        else if (await db.Clinics.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct))
            errors["name"] = [$"{name} already exists."];

        if (area is null) errors["area"] = ["Area is required."];
        else if (area.Length > 50) errors["area"] = ["Area is too long (max 50)."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    // ---------------- periods ----------------

    private static void MapPeriods(RouteGroupBuilder g)
    {
        g.MapGet("/", async (RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var periods = await db.Periods.OrderByDescending(p => p.StartDate).ToListAsync(ct);
            return periods.Select(p => p.ToDto(clock.Today));
        });

        g.MapPost("/", async (UpsertPeriodRequest req, RotaDbContext db, LocalClock clock, Notifier notify, HttpContext http,
            CancellationToken ct) =>
        {
            if (await ValidatePeriodAsync(db, req, null, ct) is { } problem) return problem;

            var period = new RotaPeriod
            {
                Name = req.Name.Trim(), StartDate = req.StartDate, EndDate = req.EndDate, LeaveDeadline = req.LeaveDeadline,
                PointsBudget = req.PointsBudget,
            };
            db.Periods.Add(period);
            string deadline = req.LeaveDeadline is { } d ? $" until {SwapEndpoints.Day(d)}" : "";
            await notify.ToOnCallOfficersAsync(Notifier.PeriodOpen, $"Enter your leave for {period.Name}",
                $"Leave for {SwapEndpoints.Day(req.StartDate)} - {SwapEndpoints.Day(req.EndDate)} is open{deadline}.", "/", email: false, ct);
            Mapping.Log(db, http, "period.create", null, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(period.ToDto(clock.Today));
        });

        g.MapPut("/{id:guid}", async (Guid id, UpsertPeriodRequest req, RotaDbContext db, LocalClock clock, HttpContext http, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();
            if (await ValidatePeriodAsync(db, req, id, ct) is { } problem) return problem;

            bool datesChanged = period.StartDate != req.StartDate || period.EndDate != req.EndDate;
            if (datesChanged && await db.Runs.AnyAsync(r => r.PeriodId == id, ct))
                return Results.Problem("Delete this period's rota drafts before changing its dates.", statusCode: StatusCodes.Status409Conflict);

            period.Name = req.Name.Trim();
            period.StartDate = req.StartDate;
            period.EndDate = req.EndDate;
            period.LeaveDeadline = req.LeaveDeadline;
            period.PointsBudget = req.PointsBudget;
            Mapping.Log(db, http, "period.update", null, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(period.ToDto(clock.Today));
        });

        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();
            if (await db.Runs.AnyAsync(r => r.PeriodId == id && r.IsPublished, ct))
                return Results.Problem("Unpublish the rota before deleting this period.", statusCode: StatusCodes.Status409Conflict);

            db.Periods.Remove(period);
            Mapping.Log(db, http, "period.delete", null, new { period.Name });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/{id:guid}/points", async (Guid id, RotaDbContext db, LeaveLimits limits, CancellationToken ct) =>
        {
            var period = await db.Periods.FindAsync([id], ct);
            if (period is null) return Results.NotFound();

            var rules = await LeaveRules.LoadAsync(db, limits, period, ct);
            var people = await db.People.Where(p => p.Status == OfficerStatus.OnCall)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
            return Results.Ok(people.Select(p =>
            {
                var leave = rules.LeaveByPerson[p.Id];
                var grant = rules.Grants.GetValueOrDefault(p.Id);
                return new PointsRowDto(p.Id, p.Code, p.Name, rules.Policy.Points(leave), rules.BaseBudget, grant?.Points ?? 0,
                    grant?.Reason, rules.Policy.Weekdays(leave), rules.Policy.WeekdayAllowance);
            }).ToList());
        });

        // Extra leave points for one officer in one period; 0 removes the grant.
        g.MapPut("/{id:guid}/points/{personId:guid}", async (Guid id, Guid personId, PointGrantRequest req, RotaDbContext db,
            Notifier notify, HttpContext http, CancellationToken ct) =>
        {
            if (!await db.Periods.AnyAsync(p => p.Id == id, ct) || !await db.People.AnyAsync(p => p.Id == personId, ct))
                return Results.NotFound();
            string? reason = Clean(req.Reason);
            if (req.Points is < 0 or > 100 || reason?.Length > 200)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["points"] = ["Extra points must be 0-100 (reason max 200)."] });

            var grant = await db.PointGrants.FindAsync([id, personId], ct);
            if (req.Points == 0) { if (grant is not null) db.PointGrants.Remove(grant); }
            else if (grant is null) db.PointGrants.Add(new PointGrant { PeriodId = id, PersonId = personId, Points = req.Points, Reason = reason });
            else { grant.Points = req.Points; grant.Reason = reason; }

            if (req.Points > (grant?.Points ?? 0))
                await notify.ToPeopleAsync([personId], Notifier.Points, $"You have {req.Points} extra leave point(s)",
                    reason is null ? "Added by the admin for this period." : $"Reason: {reason}", "/", email: false, ct);
            Mapping.Log(db, http, "points.grant", personId, new { period = id, req.Points, reason });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/lock", (Guid id, RotaDbContext db, LocalClock clock, HttpContext http, CancellationToken ct) =>
            SetStatusAsync(id, PeriodStatus.Open, PeriodStatus.Locked, db, clock, http, ct));

        g.MapPost("/{id:guid}/unlock", (Guid id, RotaDbContext db, LocalClock clock, HttpContext http, CancellationToken ct) =>
            SetStatusAsync(id, PeriodStatus.Locked, PeriodStatus.Open, db, clock, http, ct));
    }

    private static async Task<IResult> SetStatusAsync(Guid id, PeriodStatus from, PeriodStatus to, RotaDbContext db,
        LocalClock clock, HttpContext http, CancellationToken ct)
    {
        var period = await db.Periods.FindAsync([id], ct);
        if (period is null) return Results.NotFound();
        if (period.Status != from)
            return Results.Problem($"The period is {period.Status}, not {from}.", statusCode: StatusCodes.Status409Conflict);

        period.Status = to;
        Mapping.Log(db, http, $"period.{to.ToString().ToLowerInvariant()}", null, new { period.Name });
        await db.SaveChangesAsync(ct);
        return Results.Ok(period.ToDto(clock.Today));
    }

    private static async Task<IResult?> ValidatePeriodAsync(RotaDbContext db, UpsertPeriodRequest req, Guid? id, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Name)) errors["name"] = ["Name is required."];
        if (req.EndDate < req.StartDate) errors["endDate"] = ["End date is before the start date."];
        else if (req.EndDate.DayNumber - req.StartDate.DayNumber > 366) errors["endDate"] = ["A period can be at most a year."];
        if (req.LeaveDeadline > req.EndDate) errors["leaveDeadline"] = ["The deadline should be before the period ends."];
        if (req.PointsBudget is < 0 or > 100) errors["pointsBudget"] = ["Leave points must be 0-100."];

        var overlap = await db.Periods
            .Where(p => p.Id != id && p.StartDate <= req.EndDate && req.StartDate <= p.EndDate)
            .Select(p => p.Name).FirstOrDefaultAsync(ct);
        if (overlap is not null) errors["startDate"] = [$"Overlaps with {overlap}."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    // ---------------- peak days ----------------

    private static void MapPeakDays(RouteGroupBuilder g)
    {
        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
            await db.PeakDays.OrderBy(p => p.Date).Select(p => new HolidayDto(p.Date, p.Name)).ToListAsync(ct));

        g.MapPut("/", async (UpsertHolidayRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            string name = Clean(req.Name) ?? "Peak day";
            var p = await db.PeakDays.FindAsync([req.Date], ct);
            if (p is null) db.PeakDays.Add(new PeakDay { Date = req.Date, Name = name });
            else p.Name = name;
            Mapping.Log(db, http, "peak-day.upsert", null, req);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/{date}", async (DateOnly date, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            int n = await db.PeakDays.Where(p => p.Date == date).ExecuteDeleteAsync(ct);
            Mapping.Log(db, http, "peak-day.delete", null, new { date });
            await db.SaveChangesAsync(ct);
            return n > 0 ? Results.NoContent() : Results.NotFound();
        });
    }

    // ---------------- holidays ----------------

    private static void MapHolidays(RouteGroupBuilder g)
    {
        g.MapPut("/", async (UpsertHolidayRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var h = await db.Holidays.FindAsync([req.Date], ct);
            if (h is null) db.Holidays.Add(new PublicHoliday { Date = req.Date, Name = req.Name.Trim() });
            else h.Name = req.Name.Trim();
            Mapping.Log(db, http, "holiday.upsert", null, req);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/bulk", async (BulkHolidayRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (!LeaveText.TryParseDateList(req.Text, req.Reference, out var dates, out var error))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = [error!] });

            var existing = (await db.Holidays.Where(h => dates.Contains(h.Date)).Select(h => h.Date).ToListAsync(ct)).ToHashSet();
            foreach (var d in dates.Where(d => !existing.Contains(d)))
                db.Holidays.Add(new PublicHoliday { Date = d, Name = string.IsNullOrWhiteSpace(req.Name) ? "Public holiday" : req.Name.Trim() });

            Mapping.Log(db, http, "holiday.bulk", null, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { added = dates.Count - existing.Count });
        });

        g.MapDelete("/{date}", async (DateOnly date, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            int n = await db.Holidays.Where(h => h.Date == date).ExecuteDeleteAsync(ct);
            Mapping.Log(db, http, "holiday.delete", null, new { date });
            await db.SaveChangesAsync(ct);
            return n > 0 ? Results.NoContent() : Results.NotFound();
        });
    }
}

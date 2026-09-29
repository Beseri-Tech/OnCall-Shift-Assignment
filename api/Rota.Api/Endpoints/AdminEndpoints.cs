using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Leave;

namespace Rota.Api.Endpoints;

public static class AdminEndpoints
{
    public const string Policy = "Admin";
    public const string LoginRateLimit = "admin-login";

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/admin").WithTags("Admin auth");

        auth.MapPost("/login", async (LoginRequest req, HttpContext http, IConfiguration config) =>
        {
            if (!AdminPassword.Verify(req.Password, config["Admin:PasswordHash"]))
                return Results.Problem("Wrong password.", statusCode: StatusCodes.Status401Unauthorized);

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "admin")],
                CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.NoContent();
        }).RequireRateLimiting(LoginRateLimit);

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        auth.MapGet("/me", () => Results.Ok(new { admin = true })).RequireAuthorization(Policy);

        var admin = app.MapGroup("/api/admin").RequireAuthorization(Policy);
        MapPeople(admin.MapGroup("/people").WithTags("Admin people"));
        MapPeriods(admin.MapGroup("/periods").WithTags("Admin periods"));
        MapHolidays(admin.MapGroup("/holidays").WithTags("Admin holidays"));
        admin.MapRotaEndpoints();
        admin.MapImportEndpoints();
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
                return Results.Problem("This person has shifts in a rota. Deactivate them instead so their history is kept.",
                    statusCode: StatusCodes.Status409Conflict);

            db.People.Remove(person);
            Mapping.Log(db, http, "person.delete", id, new { person.Name, person.Code });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    internal static async Task<List<AdminPersonDto>> AdminPeopleAsync(RotaDbContext db, CancellationToken ct)
    {
        var totals = await Totals.ForPeopleAsync(db, ct: ct);
        var people = await db.People.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        return people.Select(p =>
        {
            var t = totals.GetValueOrDefault(p.Id, PersonTotals.Unknown);
            return new AdminPersonDto(p.Id, p.Code, p.Name, p.Active, p.SortOrder, p.ExtraShift, p.PreferWeekendHoliday,
                p.WeekendWeight, p.OpeningTotal, p.OpeningWeekday, p.OpeningWeekendHoliday, t.ToDto(), t.Known);
        }).ToList();
    }

    private static void Apply(Person p, UpsertPersonRequest req)
    {
        p.Name = Regex.Replace(req.Name.Trim(), @"\s+", " ");
        p.Active = req.Active;
        p.ExtraShift = req.ExtraShift;
        p.PreferWeekendHoliday = req.PreferWeekendHoliday;
        p.WeekendWeight = req.WeekendWeight;
        p.OpeningTotal = req.OpeningTotal;
        p.OpeningWeekday = req.OpeningWeekday;
        p.OpeningWeekendHoliday = req.OpeningWeekendHoliday;
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

        if (req.WeekendWeight is < 1 or > 5) errors["weekendWeight"] = ["Weekend weight must be 1-5."];
        if (req.OpeningTotal < 0 || req.OpeningWeekday < 0 || req.OpeningWeekendHoliday < 0)
            errors["opening"] = ["Opening numbers can't be negative."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

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

    // ---------------- periods ----------------

    private static void MapPeriods(RouteGroupBuilder g)
    {
        g.MapGet("/", async (RotaDbContext db, LocalClock clock, CancellationToken ct) =>
        {
            var periods = await db.Periods.OrderByDescending(p => p.StartDate).ToListAsync(ct);
            return periods.Select(p => p.ToDto(clock.Today));
        });

        g.MapPost("/", async (UpsertPeriodRequest req, RotaDbContext db, LocalClock clock, HttpContext http, CancellationToken ct) =>
        {
            if (await ValidatePeriodAsync(db, req, null, ct) is { } problem) return problem;

            var period = new RotaPeriod
            {
                Name = req.Name.Trim(), StartDate = req.StartDate, EndDate = req.EndDate, LeaveDeadline = req.LeaveDeadline,
            };
            db.Periods.Add(period);
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

        var overlap = await db.Periods
            .Where(p => p.Id != id && p.StartDate <= req.EndDate && req.StartDate <= p.EndDate)
            .Select(p => p.Name).FirstOrDefaultAsync(ct);
        if (overlap is not null) errors["startDate"] = [$"Overlaps with {overlap}."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
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

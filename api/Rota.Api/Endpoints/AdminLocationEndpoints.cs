using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using static Rota.Api.Endpoints.AdminEndpoints;

namespace Rota.Api.Endpoints;

/// <summary>
/// Locations: state -> district -> clinic (where officers work, shared by all hospitals) and state -> district -> hospital
/// (facility: runs a rota). States, districts and hospitals are the masters' to change.
/// </summary>
public static class AdminLocationEndpoints
{
    public static void MapLocationEndpoints(this RouteGroupBuilder admin)
    {
        MapStates(admin.MapGroup("/states").WithTags("Admin locations"));
        MapDistricts(admin.MapGroup("/districts").WithTags("Admin locations"));
        MapClinics(admin.MapGroup("/clinics").WithTags("Admin locations"));
        MapFacilities(admin.MapGroup("/facilities").WithTags("Admin locations").RequireAuthorization(Session.MasterPolicy));
    }

    /// <summary>The hospitals a user can work in: all of them for a master (the switcher), else their own.</summary>
    public static void MapFacilityList(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/facilities", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var mine = http.User.FacilityId();
            bool master = http.User.IsMaster();
            return await db.Facilities.Where(f => master || f.Id == mine)
                .OrderBy(f => f.District!.State!.Name).ThenBy(f => f.Name)
                .Select(f => new FacilityDto(f.Id, f.Name, f.Code, f.DistrictId, f.District!.Name, f.District.StateId, f.District.State!.Name,
                    db.People.IgnoreQueryFilters().Count(p => p.FacilityId == f.Id && p.Status != OfficerStatus.Left),
                    db.Accounts.IgnoreQueryFilters().Count(a => a.FacilityId == f.Id),
                    db.Periods.IgnoreQueryFilters().Where(p => p.FacilityId == f.Id).OrderByDescending(p => p.StartDate)
                        .Select(p => p.Name).FirstOrDefault(),
                    db.Periods.IgnoreQueryFilters().Where(p => p.FacilityId == f.Id).OrderByDescending(p => p.StartDate)
                        .Select(p => (PeriodStatus?)p.Status).FirstOrDefault()))
                .ToListAsync(ct);
        }).WithTags("Facilities");

    // ---------------- facilities (hospitals) ----------------

    private static void MapFacilities(RouteGroupBuilder g)
    {
        g.MapPost("/", async (UpsertFacilityRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await ValidateFacilityAsync(db, req, null, ct) is { } problem) return problem;
            var f = new Facility { Name = Clean(req.Name)!, Code = Clean(req.Code)!.ToUpperInvariant(), DistrictId = req.DistrictId };
            db.Facilities.Add(f);
            Mapping.Log(db, http, "facility.create", null, null, f.Id, after: new { f.Name, f.Code });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/facilities/{f.Id}", f.Id);
        });

        g.MapPut("/{id:guid}", async (Guid id, UpsertFacilityRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var f = await db.Facilities.FindAsync([id], ct);
            if (f is null) return Results.NotFound();
            if (await ValidateFacilityAsync(db, req, id, ct) is { } problem) return problem;
            var before = new { f.Name, f.Code, f.DistrictId };
            f.Name = Clean(req.Name)!;
            f.Code = Clean(req.Code)!.ToUpperInvariant();
            f.DistrictId = req.DistrictId;
            Mapping.Log(db, http, "facility.update", null, null, id, before, new { f.Name, f.Code, f.DistrictId });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var f = await db.Facilities.FindAsync([id], ct);
            if (f is null) return Results.NotFound();
            if (await db.People.IgnoreQueryFilters().AnyAsync(p => p.FacilityId == id, ct) ||
                await db.Periods.IgnoreQueryFilters().AnyAsync(p => p.FacilityId == id, ct) ||
                await db.Accounts.IgnoreQueryFilters().AnyAsync(a => a.FacilityId == id, ct))
                return Results.Problem($"{f.Name} still has officers, rota periods or accounts.", statusCode: StatusCodes.Status409Conflict);
            db.Facilities.Remove(f);
            Mapping.Log(db, http, "facility.delete", null, null, id, before: new { f.Name, f.Code });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult?> ValidateFacilityAsync(RotaDbContext db, UpsertFacilityRequest req, Guid? id, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string? name = Clean(req.Name), code = Clean(req.Code)?.ToUpperInvariant();
        if (name is null) errors["name"] = ["Name is required."];
        else if (name.Length > 100) errors["name"] = ["Name is too long (max 100)."];
        else if (await db.Facilities.AnyAsync(f => f.Id != id && f.Name.ToLower() == name.ToLower(), ct)) errors["name"] = [$"{name} already exists."];
        if (code is null) errors["code"] = ["Code is required (e.g. HTF)."];
        else if (code.Length > 16) errors["code"] = ["Code is too long (max 16)."];
        else if (await db.Facilities.AnyAsync(f => f.Id != id && f.Code == code, ct)) errors["code"] = [$"{code} is already used."];
        if (!await db.Districts.AnyAsync(d => d.Id == req.DistrictId, ct)) errors["districtId"] = ["Pick a district."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    // ---------------- states ----------------

    private static void MapStates(RouteGroupBuilder g)
    {
        // The whole tree above clinics: states with their districts.
        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
        {
            var states = await db.States.Include(s => s.Districts).ThenInclude(d => d.Clinics).AsSplitQuery().OrderBy(s => s.Name).ToListAsync(ct);
            return states.Select(s => new StateDto(s.Id, s.Name,
                s.Districts.OrderBy(d => d.Name).Select(d => new DistrictDto(d.Id, d.Name, d.Clinics.Count)).ToList())).ToList();
        });

        g.MapPost("/", async (UpsertStateRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await ValidateStateAsync(db, req, null, ct) is { } problem) return problem;
            var state = new State { Name = Clean(req.Name)! };
            db.States.Add(state);
            Mapping.Log(db, http, "state.create", null, null, state.Id, after: new { state.Name });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/states/{state.Id}", state.Id);
        }).RequireAuthorization(Session.MasterPolicy);

        g.MapPut("/{id:guid}", async (Guid id, UpsertStateRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var state = await db.States.FindAsync([id], ct);
            if (state is null) return Results.NotFound();
            if (await ValidateStateAsync(db, req, id, ct) is { } problem) return problem;
            var before = new { state.Name };
            state.Name = Clean(req.Name)!;
            Mapping.Log(db, http, "state.update", null, null, id, before, new { state.Name });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(Session.MasterPolicy);

        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var state = await db.States.FindAsync([id], ct);
            if (state is null) return Results.NotFound();
            if (await db.Districts.AnyAsync(d => d.StateId == id, ct))
                return Results.Problem($"Remove the districts of {state.Name} first.", statusCode: StatusCodes.Status409Conflict);
            db.States.Remove(state);
            Mapping.Log(db, http, "state.delete", null, null, id, before: new { state.Name });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(Session.MasterPolicy);
    }

    private static async Task<IResult?> ValidateStateAsync(RotaDbContext db, UpsertStateRequest req, Guid? id, CancellationToken ct)
    {
        string? name = Clean(req.Name);
        string? error = name is null ? "Name is required."
            : name.Length > 50 ? "Name is too long (max 50)."
            : await db.States.AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower(), ct) ? $"{name} already exists."
            : null;
        return error is null ? null : Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = [error] });
    }

    // ---------------- districts ----------------

    private static void MapDistricts(RouteGroupBuilder g)
    {
        g.MapPost("/", async (UpsertDistrictRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await ValidateDistrictAsync(db, req, null, ct) is { } problem) return problem;
            var district = new District { Name = Clean(req.Name)!, StateId = req.StateId };
            db.Districts.Add(district);
            Mapping.Log(db, http, "district.create", null, null, district.Id, after: await Audit.DistrictAsync(db, district, ct));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/districts/{district.Id}", district.Id);
        }).RequireAuthorization(Session.MasterPolicy);

        g.MapPut("/{id:guid}", async (Guid id, UpsertDistrictRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var district = await db.Districts.FindAsync([id], ct);
            if (district is null) return Results.NotFound();
            if (await ValidateDistrictAsync(db, req, id, ct) is { } problem) return problem;
            var before = await Audit.DistrictAsync(db, district, ct);
            district.Name = Clean(req.Name)!;
            district.StateId = req.StateId;
            Mapping.Log(db, http, "district.update", null, null, id, before, await Audit.DistrictAsync(db, district, ct));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(Session.MasterPolicy);

        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var district = await db.Districts.FindAsync([id], ct);
            if (district is null) return Results.NotFound();
            if (await db.Clinics.AnyAsync(c => c.DistrictId == id, ct))
                return Results.Problem($"Move or remove the clinics in {district.Name} first.", statusCode: StatusCodes.Status409Conflict);
            db.Districts.Remove(district);
            Mapping.Log(db, http, "district.delete", null, null, id, before: await Audit.DistrictAsync(db, district, ct));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(Session.MasterPolicy);
    }

    private static async Task<IResult?> ValidateDistrictAsync(RotaDbContext db, UpsertDistrictRequest req, Guid? id, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string? name = Clean(req.Name);

        if (!await db.States.AnyAsync(s => s.Id == req.StateId, ct)) errors["stateId"] = ["Pick a state."];
        if (name is null) errors["name"] = ["Name is required."];
        else if (name.Length > 50) errors["name"] = ["Name is too long (max 50)."];
        else if (await db.Districts.AnyAsync(d => d.Id != id && d.StateId == req.StateId && d.Name.ToLower() == name.ToLower(), ct))
            errors["name"] = [$"{name} already exists in this state."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    // ---------------- clinics ----------------

    private static void MapClinics(RouteGroupBuilder g)
    {
        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
            await db.Clinics.OrderBy(c => c.District!.State!.Name).ThenBy(c => c.District!.Name).ThenBy(c => c.Name)
                .Select(c => new ClinicDto(c.Id, c.Name, c.DistrictId, c.District!.Name, c.District.State!.Name,
                    db.People.Count(p => p.ClinicId == c.Id)))
                .ToListAsync(ct));

        g.MapPost("/", async (UpsertClinicRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await ValidateClinicAsync(db, req, null, ct) is { } problem) return problem;
            var clinic = new Clinic { Name = Clean(req.Name)!, DistrictId = req.DistrictId };
            db.Clinics.Add(clinic);
            Mapping.Log(db, http, "clinic.create", null, null, clinic.Id, after: await Audit.ClinicAsync(db, clinic, ct));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/clinics/{clinic.Id}", clinic.Id);
        });

        g.MapPut("/{id:guid}", async (Guid id, UpsertClinicRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var clinic = await db.Clinics.FindAsync([id], ct);
            if (clinic is null) return Results.NotFound();
            if (await ValidateClinicAsync(db, req, id, ct) is { } problem) return problem;
            var before = await Audit.ClinicAsync(db, clinic, ct);
            clinic.Name = Clean(req.Name)!;
            clinic.DistrictId = req.DistrictId;
            Mapping.Log(db, http, "clinic.update", null, null, id, before, await Audit.ClinicAsync(db, clinic, ct));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Dates nobody from a clinic can be on call (e.g. a clinic event). Only dates from `from` (default today) are listed.
        g.MapGet("/blocked-days", async (DateOnly? from, RotaDbContext db, CancellationToken ct) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
            return await db.ClinicBlockedDays.Where(d => d.Date >= start).OrderBy(d => d.Date)
                .Select(d => new ClinicBlockedDayDto(d.ClinicId, db.Clinics.Where(c => c.Id == d.ClinicId).Select(c => c.Name).First(), d.Date, d.Reason))
                .ToListAsync(ct);
        });

        g.MapPost("/{id:guid}/blocked-days", async (Guid id, AddClinicBlockedDaysRequest req, RotaDbContext db, HttpContext http,
            CancellationToken ct) =>
        {
            var clinic = await db.Clinics.FindAsync([id], ct);
            if (clinic is null) return Results.NotFound();
            var dates = (req.Dates ?? []).Distinct().Order().ToList();
            string? reason = Clean(req.Reason);
            var errors = new Dictionary<string, string[]>();
            if (dates.Count == 0) errors["dates"] = ["Pick at least one date."];
            if (dates.Count > 60) errors["dates"] = ["Add at most 60 dates at a time."];
            if (reason?.Length > 200) errors["reason"] = ["Reason is too long (max 200)."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var existing = await db.ClinicBlockedDays.Where(d => d.ClinicId == id && dates.Contains(d.Date)).ToListAsync(ct);
            foreach (var d in existing) d.Reason = reason;
            foreach (var date in dates.Except(existing.Select(d => d.Date)))
                db.ClinicBlockedDays.Add(new ClinicBlockedDay { ClinicId = id, Date = date, Reason = reason });
            Mapping.Log(db, http, "clinic.block-days", null, new { clinic = clinic.Name, dates, reason }, id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/{id:guid}/blocked-days/{date}", async (Guid id, DateOnly date, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var day = await db.ClinicBlockedDays.FindAsync([id, date], ct);
            if (day is null) return Results.NotFound();
            var name = await db.Clinics.Where(c => c.Id == id).Select(c => c.Name).FirstOrDefaultAsync(ct);
            db.ClinicBlockedDays.Remove(day);
            Mapping.Log(db, http, "clinic.unblock-day", null, new { clinic = name, date }, id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Officers of a deleted clinic just lose their clinic (FK is ON DELETE SET NULL).
        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var clinic = await db.Clinics.FindAsync([id], ct);
            if (clinic is null) return Results.NotFound();
            // Clinics are shared: only a master may take one away from another hospital's officers.
            if (!http.User.IsMaster() && await db.People.IgnoreQueryFilters()
                    .AnyAsync(p => p.ClinicId == id && p.FacilityId != db.FacilityId, ct))
                return Results.Problem($"Officers of another hospital work at {clinic.Name}.", statusCode: StatusCodes.Status409Conflict);
            db.Clinics.Remove(clinic);
            Mapping.Log(db, http, "clinic.delete", null, new { officers = await db.People.CountAsync(p => p.ClinicId == id, ct) }, id,
                before: await Audit.ClinicAsync(db, clinic, ct));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult?> ValidateClinicAsync(RotaDbContext db, UpsertClinicRequest req, Guid? id, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string? name = Clean(req.Name);

        if (name is null) errors["name"] = ["Name is required."];
        else if (name.Length > 100) errors["name"] = ["Name is too long (max 100)."];
        else if (await db.Clinics.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct))
            errors["name"] = [$"{name} already exists."];

        if (!await db.Districts.AnyAsync(d => d.Id == req.DistrictId, ct)) errors["districtId"] = ["Pick a district."];

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }
}

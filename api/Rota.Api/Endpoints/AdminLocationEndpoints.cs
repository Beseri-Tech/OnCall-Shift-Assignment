using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using static Rota.Api.Endpoints.AdminEndpoints;

namespace Rota.Api.Endpoints;

/// <summary>Where officers work: state -> district -> clinic. Officers belong to a clinic.</summary>
public static class AdminLocationEndpoints
{
    public static void MapLocationEndpoints(this RouteGroupBuilder admin)
    {
        MapStates(admin.MapGroup("/states").WithTags("Admin locations"));
        MapDistricts(admin.MapGroup("/districts").WithTags("Admin locations"));
        MapClinics(admin.MapGroup("/clinics").WithTags("Admin locations"));
    }

    // ---------------- states ----------------

    private static void MapStates(RouteGroupBuilder g)
    {
        // The whole tree above clinics: states with their districts.
        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
        {
            var states = await db.States.Include(s => s.Districts).ThenInclude(d => d.Clinics).OrderBy(s => s.Name).ToListAsync(ct);
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
        });

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
        });

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
        });
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
        });

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
        });

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
        });
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

        // Officers of a deleted clinic just lose their clinic (FK is ON DELETE SET NULL).
        g.MapDelete("/{id:guid}", async (Guid id, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var clinic = await db.Clinics.FindAsync([id], ct);
            if (clinic is null) return Results.NotFound();
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

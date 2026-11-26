using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;

namespace Rota.Api.Endpoints;

/// <summary>
/// The officer's own ETAP profile (IC, salary number, bank account...). Only the officer reaches it: there is no endpoint
/// that takes someone else's id, admins and masters included. Values never go into the audit log. The officer verifies it
/// with their password; any change afterwards (also to their phone or email) makes it unverified again.
/// </summary>
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/profile").WithTags("Profile").AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(ctx);
        });

        g.MapGet("/", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
            await LoadAsync(db, http, ct) is { } x ? Results.Ok(ToDto(x.Person, x.Profile)) : Forbidden());

        g.MapPut("/", async (ProfileInput input, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await LoadAsync(db, http, ct) is not { } x) return Forbidden();
            if (Validate(input) is { Count: > 0 } errors) return Results.ValidationProblem(errors);
            var p = x.Profile ?? db.Profiles.Add(new OfficerProfile { PersonId = x.Person.Id }).Entity;
            p.FullName = Upper(input.FullName);
            p.IcNo = NormalizeIc(input.IcNo);
            p.Jawatan = Upper(input.Jawatan);
            p.Gred = Upper(input.Gred);
            p.TarikhPewartaanPakar = input.TarikhPewartaanPakar;
            p.UnitTempatBertugas = Upper(input.UnitTempatBertugas);
            p.AlamatRumah = Upper(input.AlamatRumah);
            p.JarakKm = input.JarakKm;
            p.NoGaji = Clean(input.NoGaji);
            p.NoTelefonPejabat = Clean(input.NoTelefonPejabat);
            p.NoAkaunBank = Clean(input.NoAkaunBank);
            p.NamaCawanganBank = Upper(input.NamaCawanganBank);
            p.UpdatedAt = DateTimeOffset.UtcNow;
            x.Person.Phone = Clean(input.NoTelefonBimbit);
            Mapping.Log(db, http, "profile.update", x.Person.Id, null, x.Person.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(x.Person, p));
        });

        // The officer confirms the details are theirs and correct, with their password.
        g.MapPost("/verify", async (VerifyProfileRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (await LoadAsync(db, http, ct) is not { } x) return Forbidden();
            if (x.Profile is null || Missing(x.Person, x.Profile).Count > 0)
                return Results.Problem("Fill in every required field first.", statusCode: StatusCodes.Status400BadRequest);
            if (!req.Confirm)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["confirm"] = ["Tick the box to confirm."] });
            if (!Passwords.Verify(req.Password ?? "", x.Person.PasswordHash))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["Password is wrong."] });
            x.Profile.VerifiedAt = DateTimeOffset.UtcNow;
            x.Profile.VerifiedHash = Hash(x.Person, x.Profile);
            Mapping.Log(db, http, "profile.verify", x.Person.Id, null, x.Person.Id);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(x.Person, x.Profile));
        }).RequireRateLimiting(AuthEndpoints.RateLimit);
    }

    private static IResult Forbidden() =>
        Results.Problem("The ETAP profile is for officers on the rota.", statusCode: StatusCodes.Status403Forbidden);

    /// <summary>The signed-in officer and their profile (null until first saved). Null for anyone not on the rota.</summary>
    private static async Task<(Person Person, OfficerProfile? Profile)?> LoadAsync(RotaDbContext db, HttpContext http, CancellationToken ct)
    {
        if (http.User.PersonId() is not { } id || await db.People.FirstOrDefaultAsync(p => p.Id == id, ct) is not { } person) return null;
        return (person, await db.Profiles.FirstOrDefaultAsync(p => p.PersonId == id, ct));
    }

    /// <summary>The officer's profile when it is verified and unchanged since; null otherwise. For the ETAP form.</summary>
    internal static async Task<OfficerProfile?> VerifiedAsync(RotaDbContext db, Person person, CancellationToken ct) =>
        await db.Profiles.FirstOrDefaultAsync(p => p.PersonId == person.Id, ct) is { } p && IsVerified(person, p) ? p : null;

    internal static bool IsVerified(Person person, OfficerProfile p) => p.VerifiedHash is not null && p.VerifiedHash == Hash(person, p);

    private static string Hash(Person person, OfficerProfile p)
    {
        string text = string.Join('\u001f', p.FullName, p.IcNo, p.Jawatan, p.Gred,
            p.TarikhPewartaanPakar?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.UnitTempatBertugas, p.AlamatRumah,
            p.JarakKm?.ToString(CultureInfo.InvariantCulture), p.NoGaji, p.NoTelefonPejabat, p.NoAkaunBank, p.NamaCawanganBank,
            person.Phone, person.Email);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Required fields still empty (the specialist date and office phone are optional).</summary>
    private static List<string> Missing(Person person, OfficerProfile? p) =>
        new (string Field, bool Empty)[]
        {
            ("fullName", p?.FullName is null), ("icNo", p?.IcNo is null), ("jawatan", p?.Jawatan is null), ("gred", p?.Gred is null),
            ("unitTempatBertugas", p?.UnitTempatBertugas is null), ("alamatRumah", p?.AlamatRumah is null),
            ("jarakKm", p?.JarakKm is null), ("noGaji", p?.NoGaji is null), ("noTelefonBimbit", person.Phone is null),
            ("noAkaunBank", p?.NoAkaunBank is null), ("namaCawanganBank", p?.NamaCawanganBank is null),
        }.Where(x => x.Empty).Select(x => x.Field).ToList();

    private static ProfileDto ToDto(Person person, OfficerProfile? p) => new(
        p?.FullName, p?.IcNo, p?.Jawatan, p?.Gred, p?.TarikhPewartaanPakar, p?.UnitTempatBertugas, p?.AlamatRumah, p?.JarakKm,
        p?.NoGaji, p?.NoTelefonPejabat, person.Phone, person.Email, p?.NoAkaunBank, p?.NamaCawanganBank,
        p is not null && IsVerified(person, p), p?.VerifiedAt, Missing(person, p));

    private static Dictionary<string, string[]> Validate(ProfileInput i)
    {
        var errors = new Dictionary<string, string[]>();
        void Max(string key, string? v, int max) { if (v?.Trim().Length > max) errors[key] = [$"Too long (max {max} characters)."]; }
        Max("fullName", i.FullName, 200); Max("jawatan", i.Jawatan, 100); Max("gred", i.Gred, 20);
        Max("unitTempatBertugas", i.UnitTempatBertugas, 300); Max("alamatRumah", i.AlamatRumah, 400); Max("noGaji", i.NoGaji, 30);
        Max("noTelefonPejabat", i.NoTelefonPejabat, 40); Max("noTelefonBimbit", i.NoTelefonBimbit, 32);
        Max("noAkaunBank", i.NoAkaunBank, 30); Max("namaCawanganBank", i.NamaCawanganBank, 150);
        if (NormalizeIc(i.IcNo) is { } ic && !Regex.IsMatch(ic, @"^\d{12}$")) errors["icNo"] = ["IC number is 12 digits (dashes are fine)."];
        if (i.JarakKm is < 0 or > 10000) errors["jarakKm"] = ["Distance is in km (0-10000)."];
        if (Clean(i.NoAkaunBank) is { } acc && !Regex.IsMatch(acc, @"^[\d -]{6,30}$")) errors["noAkaunBank"] = ["Account number is digits."];
        return errors;
    }

    private static string? NormalizeIc(string? s) => Clean(s) is { } t ? Regex.Replace(t, @"[\s-]", "") : null;
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : Regex.Replace(s.Trim(), @"[ \t]+", " ");
    private static string? Upper(string? s) => Clean(s)?.ToUpperInvariant();
}

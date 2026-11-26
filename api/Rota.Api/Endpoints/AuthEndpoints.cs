using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

/// <summary>Login, logout, first-login password change and self-service reset by email.</summary>
public static class AuthEndpoints
{
    public const string RateLimit = "auth";

    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapPost("/login", async (LoginRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            string email = NormalizeEmail(req.Email);
            var account = await db.People.FirstOrDefaultAsync(a => a.Email == email, ct);
            if (account?.PasswordHash is null || !Passwords.Verify(req.Password, account.PasswordHash))
                return Results.Problem("Wrong email or password.", statusCode: StatusCodes.Status401Unauthorized);
            if (!account.Enabled || account.Status == OfficerStatus.Left)
                return Results.Problem("This account is disabled. Contact the admin.", statusCode: StatusCodes.Status403Forbidden);
            if (account.MustChangePassword && account.TempPasswordExpiresAt < DateTimeOffset.UtcNow)
                return Results.Problem("Your temporary password has expired. Ask the admin to send a new invite.",
                    statusCode: StatusCodes.Status403Forbidden);

            account.LastLoginAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await Session.SignInAsync(http, account);
            return Results.Ok(await ToMeAsync(db, account, account.FacilityId, ct));
        }).AllowAnonymous().RequireRateLimiting(RateLimit);

        g.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapGet("/me", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var account = await CurrentAsync(db, http, ct);
            return account is null ? Results.Unauthorized() : Results.Ok(await ToMeAsync(db, account, http.User.FacilityId(), ct));
        }).RequireAuthorization(Session.SignedInPolicy);

        // A master picks the hospital they work in (null = none, to manage hospitals and masters).
        g.MapPost("/facility", async (SetFacilityRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var account = await CurrentAsync(db, http, ct);
            if (account is null) return Results.Unauthorized();
            if (req.FacilityId is { } fid && !await db.Facilities.AnyAsync(f => f.Id == fid, ct)) return Results.NotFound();
            await Session.SignInAsync(http, account, req.FacilityId);
            return Results.Ok(await ToMeAsync(db, account, req.FacilityId, ct));
        }).RequireAuthorization(Session.MasterPolicy);

        g.MapPost("/change-password", async (ChangePasswordRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var account = await CurrentAsync(db, http, ct);
            if (account is null) return Results.Unauthorized();
            if (!Passwords.Verify(req.CurrentPassword, account.PasswordHash))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["Current password is wrong."] });
            if (NewPasswordError(req.NewPassword, req.CurrentPassword) is { } error)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [error] });

            SetPassword(account, req.NewPassword);
            Mapping.Log(db, http, "account.change-password", account.OnRota ? account.Id : null, null, account.Id);
            await db.SaveChangesAsync(ct);
            await Session.SignInAsync(http, account, http.User.FacilityId());   // new stamp, no temporary-password flag
            return Results.Ok(await ToMeAsync(db, account, http.User.FacilityId(), ct));
        }).RequireAuthorization(Session.SignedInPolicy);

        // Always 204 so the form can't be used to find out which emails have accounts.
        g.MapPost("/forgot", async (ForgotPasswordRequest req, RotaDbContext db, Mailer mailer, HttpContext http, CancellationToken ct) =>
        {
            string email = NormalizeEmail(req.Email);
            var account = await db.People.FirstOrDefaultAsync(a => a.Email == email && a.Enabled, ct);
            if (account is null) return Results.NoContent();

            var (token, hash) = Passwords.NewResetToken();
            account.ResetTokenHash = hash;
            account.ResetTokenExpiresAt = DateTimeOffset.UtcNow + ResetTokenLifetime;

            string link = $"{mailer.BaseUrl(http)}/reset-password?token={token}";
            mailer.Enqueue(db, account.Email!, new EmailContent(
                Subject: "Reset your On-Call Rota password",
                Heading: "Reset your password",
                Paragraphs: [$"We received a request to reset the password for {account.Email}."],
                ButtonText: "Choose a new password",
                ButtonUrl: link,
                Note: $"This link works once and expires in {ResetTokenLifetime.TotalMinutes:0} minutes. " +
                      "If you didn't ask for this, you can ignore this email; your password won't change."), http);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting(RateLimit);

        g.MapPost("/reset", async (ResetPasswordRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            string hash = Passwords.HashToken(req.Token ?? "");
            var account = await db.People.FirstOrDefaultAsync(a => a.ResetTokenHash == hash && a.Enabled, ct);
            if (account is null || account.ResetTokenExpiresAt < DateTimeOffset.UtcNow)
                return Results.Problem("This reset link is invalid or has expired. Ask for a new one.", statusCode: StatusCodes.Status400BadRequest);
            if (NewPasswordError(req.NewPassword, null) is { } error)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [error] });

            SetPassword(account, req.NewPassword);
            Mapping.Log(db, http, "account.reset-password", account.OnRota ? account.Id : null, new { account.Email }, account.Id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting(RateLimit);
    }

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static Task<Person?> CurrentAsync(RotaDbContext db, HttpContext http, CancellationToken ct)
    {
        var id = http.User.AccountId();
        // A master's own account belongs to no hospital, so it is outside the filter of the hospital they picked.
        return db.People.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    private static async Task<MeDto> ToMeAsync(RotaDbContext db, Person a, Guid? facilityId, CancellationToken ct) =>
        new(a.Id, a.Email ?? "", a.Role, a.OnRota ? a.Id : null, a.Name, a.MustChangePassword,
            facilityId is null ? null : await db.Facilities.Where(f => f.Id == facilityId)
                .Select(f => new { f.Id, f.Name, f.Code, f.District!.State!.WeekendDays }).FirstOrDefaultAsync(ct) is { } f
                ? new FacilityRef(f.Id, f.Name, f.Code, Rota.Core.Leave.BlockedDays.FromMask(f.WeekendDays)) : null);

    private static string? NewPasswordError(string? password, string? current) =>
        (password?.Length ?? 0) < Passwords.MinLength ? $"Use at least {Passwords.MinLength} characters."
        : password == current ? "Choose a password different from the current one."
        : null;

    /// <summary>Sets a password chosen by the user: clears temporary-password and reset state, ends other sessions.</summary>
    private static void SetPassword(Person a, string password)
    {
        a.PasswordHash = Passwords.Hash(password);
        a.MustChangePassword = false;
        a.TempPasswordExpiresAt = null;
        a.ResetTokenHash = null;
        a.ResetTokenExpiresAt = null;
        a.SecurityStamp = Guid.NewGuid().ToString("N");
    }
}

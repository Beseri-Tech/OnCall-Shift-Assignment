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
            var account = await db.Accounts.Include(a => a.Person).FirstOrDefaultAsync(a => a.Email == email, ct);
            if (account is null || !Passwords.Verify(req.Password, account.PasswordHash))
                return Results.Problem("Wrong email or password.", statusCode: StatusCodes.Status401Unauthorized);
            if (!account.Enabled || account.Person?.Status == OfficerStatus.Left)
                return Results.Problem("This account is disabled. Contact the admin.", statusCode: StatusCodes.Status403Forbidden);
            if (account.MustChangePassword && account.TempPasswordExpiresAt < DateTimeOffset.UtcNow)
                return Results.Problem("Your temporary password has expired. Ask the admin to send a new invite.",
                    statusCode: StatusCodes.Status403Forbidden);

            account.LastLoginAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await Session.SignInAsync(http, account);
            return Results.Ok(ToMe(account));
        }).AllowAnonymous().RequireRateLimiting(RateLimit);

        g.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapGet("/me", async (RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var account = await CurrentAsync(db, http, ct);
            return account is null ? Results.Unauthorized() : Results.Ok(ToMe(account));
        }).RequireAuthorization(Session.SignedInPolicy);

        g.MapPost("/change-password", async (ChangePasswordRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var account = await CurrentAsync(db, http, ct);
            if (account is null) return Results.Unauthorized();
            if (!Passwords.Verify(req.CurrentPassword, account.PasswordHash))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["Current password is wrong."] });
            if (NewPasswordError(req.NewPassword, req.CurrentPassword) is { } error)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [error] });

            SetPassword(account, req.NewPassword);
            Mapping.Log(db, http, "account.change-password", account.PersonId, null);
            await db.SaveChangesAsync(ct);
            await Session.SignInAsync(http, account);   // new stamp, no temporary-password flag
            return Results.Ok(ToMe(account));
        }).RequireAuthorization(Session.SignedInPolicy);

        // Always 204 so the form can't be used to find out which emails have accounts.
        g.MapPost("/forgot", async (ForgotPasswordRequest req, RotaDbContext db, Mailer mailer, HttpContext http, CancellationToken ct) =>
        {
            string email = NormalizeEmail(req.Email);
            var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == email && a.Enabled, ct);
            if (account is null) return Results.NoContent();

            var (token, hash) = Passwords.NewResetToken();
            account.ResetTokenHash = hash;
            account.ResetTokenExpiresAt = DateTimeOffset.UtcNow + ResetTokenLifetime;

            string link = $"{mailer.BaseUrl(http)}/reset-password?token={token}";
            mailer.Enqueue(db, account.Email, new EmailContent(
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
            var account = await db.Accounts.FirstOrDefaultAsync(a => a.ResetTokenHash == hash && a.Enabled, ct);
            if (account is null || account.ResetTokenExpiresAt < DateTimeOffset.UtcNow)
                return Results.Problem("This reset link is invalid or has expired. Ask for a new one.", statusCode: StatusCodes.Status400BadRequest);
            if (NewPasswordError(req.NewPassword, null) is { } error)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [error] });

            SetPassword(account, req.NewPassword);
            Mapping.Log(db, http, "account.reset-password", account.PersonId, new { account.Email });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting(RateLimit);
    }

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static Task<Account?> CurrentAsync(RotaDbContext db, HttpContext http, CancellationToken ct)
    {
        var id = http.User.AccountId();
        return db.Accounts.Include(a => a.Person).FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    private static MeDto ToMe(Account a) =>
        new(a.Id, a.Email, a.Role, a.PersonId, a.Person?.Name, a.MustChangePassword);

    private static string? NewPasswordError(string? password, string? current) =>
        (password?.Length ?? 0) < Passwords.MinLength ? $"Use at least {Passwords.MinLength} characters."
        : password == current ? "Choose a password different from the current one."
        : null;

    /// <summary>Sets a password chosen by the user: clears temporary-password and reset state, ends other sessions.</summary>
    private static void SetPassword(Account a, string password)
    {
        a.PasswordHash = Passwords.Hash(password);
        a.MustChangePassword = false;
        a.TempPasswordExpiresAt = null;
        a.ResetTokenHash = null;
        a.ResetTokenExpiresAt = null;
        a.SecurityStamp = Guid.NewGuid().ToString("N");
    }
}

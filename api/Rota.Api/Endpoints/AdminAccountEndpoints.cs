using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

/// <summary>Invites (temporary password by email), resends (also the admin's password reset) and role changes.</summary>
public static class AdminAccountEndpoints
{
    private static readonly TimeSpan TempPasswordLifetime = TimeSpan.FromDays(7);

    public static void MapAccountEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/accounts").WithTags("Admin accounts");

        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
            await db.Accounts.OrderBy(a => a.Email)
                .Select(a => new AccountDto(a.Id, a.Email, a.Role, a.PersonId, a.Person != null ? a.Person.Name : null, a.Enabled,
                    a.MustChangePassword, a.TempPasswordExpiresAt, a.LastLoginAt))
                .ToListAsync(ct));

        g.MapPost("/", async (InviteRequest req, RotaDbContext db, Mailer mailer, HttpContext http, CancellationToken ct) =>
        {
            string email = AuthEndpoints.NormalizeEmail(req.Email);
            if (await ValidateAsync(db, email, req.Role, req.PersonId, null, ct) is { } problem) return problem;

            var result = Invite(db, mailer, http, email, req.Role, req.Role == AccountRole.Supervisor ? null : req.PersonId);
            await db.SaveChangesAsync(ct);
            return Results.Ok(result);
        });

        // New temporary password: re-sends a lost invite, and is how the admin resets someone's password.
        g.MapPost("/{id:guid}/resend", async (Guid id, RotaDbContext db, Mailer mailer, HttpContext http, CancellationToken ct) =>
        {
            var account = await db.Accounts.FindAsync([id], ct);
            if (account is null) return Results.NotFound();

            string temp = SetTemporaryPassword(account);
            bool sent = QueueInvite(db, mailer, http, account, temp);
            Mapping.Log(db, http, "account.resend", account.PersonId, new { account.Email });
            await db.SaveChangesAsync(ct);

            return Results.Ok(new InviteResult(account.Id, sent, sent ? null : temp));
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateAccountRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var account = await db.Accounts.FindAsync([id], ct);
            if (account is null) return Results.NotFound();

            // Blocking self-demotion/disable also guarantees at least one enabled admin remains.
            bool stillAdmin = req.Role is AccountRole.Admin or AccountRole.Supervisor;
            if (id == http.User.AccountId() && (!req.Enabled || !stillAdmin))
                return Results.Problem("You can't disable your own account or remove your own admin rights.",
                    statusCode: StatusCodes.Status409Conflict);

            string email = AuthEndpoints.NormalizeEmail(req.Email);
            if (await ValidateAsync(db, email, req.Role, req.PersonId, id, ct) is { } problem) return problem;

            account.Email = email;
            account.Role = req.Role;
            account.PersonId = req.Role == AccountRole.Supervisor ? null : req.PersonId;
            account.Enabled = req.Enabled;
            account.SecurityStamp = Guid.NewGuid().ToString("N");   // new rights apply at their next request
            Mapping.Log(db, http, "account.update", account.PersonId, new { email, req.Role, req.Enabled });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    /// <summary>Adds the account with a temporary password and queues the invite email; the caller saves.</summary>
    internal static InviteResult Invite(RotaDbContext db, Mailer mailer, HttpContext http, string email, AccountRole role, Guid? personId)
    {
        var account = new Account { Email = email, Role = role, PersonId = personId };
        db.Accounts.Add(account);
        string temp = SetTemporaryPassword(account);
        bool sent = QueueInvite(db, mailer, http, account, temp);
        Mapping.Log(db, http, "account.invite", personId, new { email, role });
        return new InviteResult(account.Id, sent, sent ? null : temp);
    }

    /// <summary>Problem with a normalized email (invalid, or another account has it), else null.</summary>
    internal static async Task<string?> EmailErrorAsync(RotaDbContext db, string email, Guid? accountId, CancellationToken ct) =>
        !IsValidEmail(email) ? "Enter a valid email address."
        : await db.Accounts.AnyAsync(a => a.Id != accountId && a.Email == email, ct) ? $"{email} already has an account."
        : null;

    internal static bool IsValidEmail(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Length <= 254;

    private static async Task<IResult?> ValidateAsync(RotaDbContext db, string email, AccountRole role, Guid? personId, Guid? id,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (await EmailErrorAsync(db, email, id, ct) is { } emailError) errors["email"] = [emailError];

        if (!Enum.IsDefined(role)) errors["role"] = ["Unknown role."];
        else if (role != AccountRole.Supervisor)
        {
            if (personId is null)
                errors["personId"] = ["Pick the officer this account belongs to (supervisors have none)."];
            else if (!await db.People.AnyAsync(p => p.Id == personId && p.Status != OfficerStatus.Left, ct))
                errors["personId"] = ["Unknown officer, or the officer has left."];
            else if (await db.Accounts.AnyAsync(a => a.Id != id && a.PersonId == personId, ct))
                errors["personId"] = ["This officer already has an account."];
        }

        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static string SetTemporaryPassword(Account a)
    {
        string temp = Passwords.NewTemporary();
        a.PasswordHash = Passwords.Hash(temp);
        a.MustChangePassword = true;
        a.TempPasswordExpiresAt = DateTimeOffset.UtcNow + TempPasswordLifetime;
        a.ResetTokenHash = null;
        a.ResetTokenExpiresAt = null;
        a.SecurityStamp = Guid.NewGuid().ToString("N");
        return temp;
    }

    private static bool QueueInvite(RotaDbContext db, Mailer mailer, HttpContext http, Account a, string temp) =>
        mailer.Enqueue(db, a.Email, new EmailContent(
            Subject: "You've been invited to On-Call Rota",
            Heading: "You've been invited to On-Call Rota",
            Paragraphs:
            [
                "You have been invited to On-Call Rota, the on-call roster and leave planner for dental officers in Perlis.",
                "Use it to apply for leave, see your on-call dates and swap shifts with colleagues.",
                "Sign in with the details below:",
            ],
            ButtonText: "Sign in to On-Call Rota",
            ButtonUrl: $"{mailer.BaseUrl(http)}/login",
            Details: [("Email", a.Email), ("Temporary password", temp)],
            Note: $"You'll be asked to choose your own password when you first sign in. " +
                  $"The temporary password expires in {TempPasswordLifetime.TotalDays:0} days."), http);
}

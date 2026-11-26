using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

/// <summary>
/// Sign-in for people: invites and new temporary passwords (also the admin's password reset). A hospital's people are
/// added and edited on the Officers tab (people endpoints); this group manages the masters, who belong to no hospital.
/// </summary>
public static class AdminAccountEndpoints
{
    private static readonly TimeSpan TempPasswordLifetime = TimeSpan.FromDays(7);

    public static void MapAccountEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/accounts").WithTags("Admin accounts").RequireAuthorization(Session.MasterPolicy);

        g.MapGet("/", async (RotaDbContext db, CancellationToken ct) =>
            await db.People.IgnoreQueryFilters().Where(p => p.Role == AccountRole.Master).OrderBy(p => p.Name)
                .Select(p => new AccountDto(p.Id, p.Email ?? "", p.Role, null, p.Name, p.Enabled,
                    p.MustChangePassword, p.TempPasswordExpiresAt, p.LastLoginAt, null, null))
                .ToListAsync(ct));

        g.MapPost("/", async (MasterRequest req, RotaDbContext db, Mailer mailer, HttpContext http, CancellationToken ct) =>
        {
            string email = AuthEndpoints.NormalizeEmail(req.Email);
            if (await ValidateMasterAsync(db, req, email, null, ct) is { } problem) return problem;
            var master = new Person
            {
                Name = AdminEndpoints.Clean(req.Name)!, Email = email, Role = AccountRole.Master,
                Code = "M" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant(),
            };
            db.People.Add(master);
            Mapping.Log(db, http, "account.invite", null, null, master.Id, after: new { master.Name, master.Email, master.Role });
            var result = Invite(db, mailer, http, master);
            await db.SaveChangesAsync(ct);
            return Results.Ok(result);
        });

        g.MapPut("/{id:guid}", async (Guid id, MasterRequest req, RotaDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var master = await db.People.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id && p.Role == AccountRole.Master, ct);
            if (master is null) return Results.NotFound();
            if (id == http.User.AccountId() && !req.Enabled)
                return Results.Problem("You can't disable your own account.", statusCode: StatusCodes.Status409Conflict);
            string email = AuthEndpoints.NormalizeEmail(req.Email);
            if (await ValidateMasterAsync(db, req, email, id, ct) is { } problem) return problem;

            var before = new { master.Name, master.Email, master.Enabled };
            master.Name = AdminEndpoints.Clean(req.Name)!;
            master.Email = email;
            if (master.Enabled != req.Enabled) master.SecurityStamp = Guid.NewGuid().ToString("N");
            master.Enabled = req.Enabled;
            Mapping.Log(db, http, "account.update", null, null, id, before, new { master.Name, master.Email, master.Enabled });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/resend", (Guid id, RotaDbContext db, Mailer mailer, HttpContext http, CancellationToken ct) =>
            ResendAsync(id, db, mailer, http, ct));
    }

    /// <summary>
    /// New temporary password, emailed (or returned when email is off): re-sends a lost invite, invites someone who never
    /// was, and is how an admin resets a password. A master reaches anyone; others only their hospital's people.
    /// </summary>
    internal static async Task<IResult> ResendAsync(Guid id, RotaDbContext db, Mailer mailer, HttpContext http, CancellationToken ct)
    {
        var person = await (http.User.IsMaster() ? db.People.IgnoreQueryFilters() : db.People).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is null) return Results.NotFound();
        if (person.Email is null)
            return Results.Problem($"Add {person.Name}'s email first.", statusCode: StatusCodes.Status400BadRequest);
        if (person.Status == OfficerStatus.Left)
            return Results.Problem($"{person.Name} has left and can't sign in.", statusCode: StatusCodes.Status400BadRequest);
        var result = Invite(db, mailer, http, person);
        Mapping.Log(db, http, "account.resend", person.OnRota ? person.Id : null, new { person.Email }, person.Id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }

    /// <summary>Gives the person (who has an email) a temporary password and queues the invite email; the caller saves.</summary>
    internal static InviteResult Invite(RotaDbContext db, Mailer mailer, HttpContext http, Person person)
    {
        string temp = SetTemporaryPassword(person);
        bool sent = QueueInvite(db, mailer, http, person, temp);
        return new InviteResult(person.Id, sent, sent ? null : temp);
    }

    private static async Task<IResult?> ValidateMasterAsync(RotaDbContext db, MasterRequest req, string email, Guid? id, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string? name = AdminEndpoints.Clean(req.Name);
        if (name is null) errors["name"] = ["Name is required."];
        else if (name.Length > 200) errors["name"] = ["Name is too long."];
        if (await EmailErrorAsync(db, email, id, ct) is { } emailError) errors["email"] = [emailError];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    /// <summary>Problem with a normalized email (missing, invalid, or someone else has it), else null. Emails are unique app-wide.</summary>
    internal static async Task<string?> EmailErrorAsync(RotaDbContext db, string email, Guid? personId, CancellationToken ct) =>
        email.Length == 0 ? "Email is required: it's how they sign in."
        : !IsValidEmail(email) ? "Enter a valid email address."
        : await db.People.IgnoreQueryFilters().AnyAsync(p => p.Id != personId && p.Email == email, ct) ? $"{email} is already used by someone else."
        : null;

    internal static bool IsValidEmail(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Length <= 254;

    private static string SetTemporaryPassword(Person a)
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

    private static bool QueueInvite(RotaDbContext db, Mailer mailer, HttpContext http, Person a, string temp) =>
        mailer.Enqueue(db, a.Email!, new EmailContent(
            Subject: "You've been invited to On-Call Rota",
            Heading: "You've been invited to On-Call Rota",
            Paragraphs:
            [
                "You have been invited to On-Call Rota, the on-call roster and leave planner for dental officers" +
                (a.FacilityId is { } fid && db.Facilities.Find(fid)?.Name is { } name
                    ? $" at {name}." : "."),
                "Use it to apply for leave, see your on-call dates and swap shifts with colleagues.",
                "Sign in with the details below:",
            ],
            ButtonText: "Sign in to On-Call Rota",
            ButtonUrl: $"{mailer.BaseUrl(http)}/login",
            Details: [("Email", a.Email!), ("Temporary password", temp)],
            Note: $"You'll be asked to choose your own password when you first sign in. " +
                  $"The temporary password expires in {TempPasswordLifetime.TotalDays:0} days."), http);
}

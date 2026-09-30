using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Auth;

/// <summary>The login cookie: who is signed in, and a per-request check that the account is still allowed in.</summary>
public static class Session
{
    public const string AdminPolicy = "Admin";

    /// <summary>Signed in, even with a temporary password (for /me, change-password and logout).</summary>
    public const string SignedInPolicy = "SignedIn";

    private const string AccountIdClaim = "aid";
    private const string PersonIdClaim = "pid";
    private const string StampClaim = "stamp";
    private const string MustChangeClaim = "must_change";

    public static Guid? AccountId(this ClaimsPrincipal user) => GuidClaim(user, AccountIdClaim);
    public static Guid? PersonId(this ClaimsPrincipal user) => GuidClaim(user, PersonIdClaim);
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(nameof(AccountRole.Admin)) || user.IsInRole(nameof(AccountRole.Supervisor));
    public static bool MustChangePassword(this ClaimsPrincipal user) => user.HasClaim(MustChangeClaim, "1");

    private static Guid? GuidClaim(ClaimsPrincipal user, string type) =>
        Guid.TryParse(user.FindFirstValue(type), out var id) ? id : null;

    public static Task SignInAsync(HttpContext http, Account a)
    {
        var claims = new List<Claim>
        {
            new(AccountIdClaim, a.Id.ToString()),
            new(ClaimTypes.Name, a.Email),
            new(ClaimTypes.Role, a.Role.ToString()),
            new(StampClaim, a.SecurityStamp),
        };
        if (a.PersonId is { } pid) claims.Add(new(PersonIdClaim, pid.ToString()));
        if (a.MustChangePassword) claims.Add(new(MustChangeClaim, "1"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Runs on every request with a cookie: a disabled account, a changed password/role (new stamp) or an officer
    /// marked as Left ends the session straight away.
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        var user = ctx.Principal;
        var id = user?.AccountId();
        string? stamp = user?.FindFirstValue(StampClaim);
        var db = ctx.HttpContext.RequestServices.GetRequiredService<RotaDbContext>();

        bool ok = id is not null && await db.Accounts.AnyAsync(a =>
            a.Id == id && a.Enabled && a.SecurityStamp == stamp &&
            (a.PersonId == null || a.Person!.Status != OfficerStatus.Left));
        if (!ok)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

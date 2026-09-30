using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Endpoints;
using Rota.Api.Services;
using Rota.Core.Leave;

// `dotnet Rota.Api.dll hash-password` prints a hash for Admin__PasswordHash (the first admin's password).
if (args.Length > 0 && args[0] == "hash-password")
{
    string password;
    if (args.Length > 1)
    {
        password = args[1];
    }
    else
    {
        Console.Write("Admin password: ");
        password = ReadHidden();
        Console.WriteLine();
    }
    Console.WriteLine(Passwords.Hash(password));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RotaDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Rota")
                ?? throw new InvalidOperationException("ConnectionStrings:Rota is not configured.")));

builder.Services.AddDataProtection()
    .SetApplicationName("OnCallRota")
    .PersistKeysToDbContext<RotaDbContext>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LocalClock>();
builder.Services.AddSingleton<Mailer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Mailer>());   // sends queued email in the background
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Notifier>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Leave").Get<LeaveLimits>() ?? new LeaveLimits());
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "rota_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
        // API: answer 401/403 instead of redirecting to a login page.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        o.Events.OnValidatePrincipal = Session.ValidateAsync;
    });
// Everything needs a signed-in user who has replaced their temporary password, unless an endpoint says otherwise.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().RequireAssertion(c => !c.User.MustChangePassword()).Build())
    .AddPolicy(Session.SignedInPolicy, p => p.RequireAuthenticatedUser())
    .AddPolicy(Session.AdminPolicy, p => p.RequireAuthenticatedUser()
        .RequireRole(nameof(AccountRole.Admin), nameof(AccountRole.Supervisor))
        .RequireAssertion(c => !c.User.MustChangePassword()));

builder.Services.AddCors();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.RateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("Auth:AttemptsPerMinute", 5),
            Window = TimeSpan.FromMinutes(1),
        }));
});

// Behind a reverse proxy (Caddy/nginx) so client IPs in the change log are real.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<RotaDbContext>().Database.MigrateAsync();
}

await BootstrapAdminAsync(app);

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// index.html must be revalidated so a deploy reaches browsers; the hashed assets it points to can be cached.
var staticFiles = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    },
};
app.UseDefaultFiles();
app.UseStaticFiles(staticFiles);

// The web app can be hosted separately (e.g. Cloudflare Pages on a sibling subdomain, so the SameSite=Strict cookie
// still counts as same-site). App:WebOrigin lists the origins allowed to call the API with the cookie, comma-separated.
// Before the rate limiter and auth, so preflights and 401/429 responses carry the CORS headers too.
string[] webOrigins = (app.Configuration["App:WebOrigin"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(o => o.TrimEnd('/')).ToArray();
app.UseCors(p => p.WithOrigins(webOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapPublicEndpoints();
app.MapSwapEndpoints();
app.MapNotificationEndpoints();
app.MapAdminEndpoints();

// Unknown /api routes are 404s; everything else is the React app (which shows the login page itself).
app.MapFallback("/api/{**rest}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html", staticFiles).AllowAnonymous();

app.Run();

// The first admin comes from config (Admin:Email + Admin:PasswordHash) and is only created while no admin exists,
// so it can't be used to take over once real accounts are set up.
static async Task BootstrapAdminAsync(WebApplication app)
{
    string email = AuthEndpoints.NormalizeEmail(app.Configuration["Admin:Email"]);
    string? hash = app.Configuration["Admin:PasswordHash"];

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();
    if (!await db.Database.CanConnectAsync() || await db.Accounts.AnyAsync(a => a.Role != AccountRole.Officer)) return;

    if (email.Length == 0 || string.IsNullOrEmpty(hash))
    {
        app.Logger.LogWarning("No admin account yet. Set Admin__Email and Admin__PasswordHash (`dotnet Rota.Api.dll hash-password`) and restart.");
        return;
    }

    if (await db.Accounts.AnyAsync(a => a.Email == email))
    {
        app.Logger.LogWarning("Admin:Email {Email} already belongs to an officer account; no admin was created.", email);
        return;
    }

    db.Accounts.Add(new Account { Email = email, PasswordHash = hash, Role = AccountRole.Supervisor });
    await db.SaveChangesAsync();
    app.Logger.LogInformation("Created the first admin account {Email}.", email);
}

static string ReadHidden()
{
    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
        else chars.Add(key.KeyChar);
    }
    return new string(chars.ToArray());
}

public partial class Program;

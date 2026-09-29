using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Data;
using Rota.Api.Endpoints;
using Rota.Api.Services;

// `dotnet Rota.Api.dll hash-password` prints a hash for Admin__PasswordHash.
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
    Console.WriteLine(AdminPassword.Hash(password));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RotaDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Rota")
                ?? throw new InvalidOperationException("ConnectionStrings:Rota is not configured.")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LocalClock>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "rota_admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
        // API: answer 401/403 instead of redirecting to a login page.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminEndpoints.Policy, p => p.RequireRole("admin"));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AdminEndpoints.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
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

if (string.IsNullOrEmpty(app.Configuration["Admin:PasswordHash"]))
    app.Logger.LogWarning("Admin:PasswordHash is not set - the admin page cannot be used. Run `dotnet Rota.Api.dll hash-password`.");

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapPublicEndpoints();
app.MapAdminEndpoints();

// Unknown /api routes are 404s; everything else is the React app.
app.MapFallback("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

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

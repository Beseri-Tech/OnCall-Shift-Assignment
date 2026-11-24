using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Rota.Api.Auth;
using Rota.Api.Data;
using Testcontainers.PostgreSql;

namespace Rota.Api.Tests;

/// <summary>One Postgres container + app per test class; each test uses a fresh database.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPasswordText = "test-admin-password";
    public const string OfficerPasswordText = "officer-password-123";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task InitializeAsync() => await _db.StartAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    /// <summary>A new app instance against a new, migrated database.</summary>
    public WebApplicationFactory<Program> CreateApp(Dictionary<string, string?>? settings = null)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(_db.GetConnectionString())
        {
            Database = "rota_" + Guid.NewGuid().ToString("N")[..12],
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Rota"] = builder.ConnectionString,
                ["Database:MigrateOnStartup"] = "true",
                ["Admin:Email"] = AdminEmail,
                ["Auth:AttemptsPerMinute"] = "1000",   // tests log in many officers from one address
                ["Admin:PasswordHash"] = Passwords.Hash(AdminPasswordText),
            }.Concat(settings ?? [])));
        });
    }

    public static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> app, Guid? facilityId = null)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = AdminPasswordText });
        res.EnsureSuccessStatusCode();
        // The configured admin is a master: work in the hospital all existing data belongs to.
        (await client.PostAsJsonAsync("/api/auth/facility", new SetFacilityRequest(facilityId ?? Facility.HtfId))).EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>A sign-in email for a test person's name, e.g. "Dr Aminah" -> dr-aminah@test.local.</summary>
    public static string EmailFor(string name) => Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') + "@test.local";

    /// <summary>Adds officers by name (one per line), each with an email made from their name.</summary>
    public static async Task AddPeopleAsync(HttpClient admin, string names)
    {
        foreach (var name in names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            (await admin.PostAsJsonAsync("/api/admin/people", new UpsertPersonRequest(name, null, Email: EmailFor(name)), Json))
                .EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Gives the person a new temporary password (no SMTP in tests, so it comes back), logs in as them and sets a password.
    /// </summary>
    public static async Task<HttpClient> OfficerClientAsync(WebApplicationFactory<Program> app, HttpClient admin, Guid personId)
    {
        var people = await admin.GetFromJsonAsync<List<AdminPersonDto>>("/api/admin/people", Json);
        string email = people!.Single(p => p.Id == personId).Email!;
        var invite = await admin.PostAsync($"/api/admin/people/{personId}/resend", null);
        invite.EnsureSuccessStatusCode();
        string temp = (await invite.Content.ReadFromJsonAsync<InviteResult>(Json))!.TempPassword!;

        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = temp })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(temp, OfficerPasswordText)))
            .EnsureSuccessStatusCode();
        return client;
    }
}

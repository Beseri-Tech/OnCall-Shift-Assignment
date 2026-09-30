using System.Net.Http.Json;
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

    public static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = AdminPasswordText });
        res.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Invites the officer (no SMTP in tests, so the temporary password comes back), logs in and sets a password.</summary>
    public static async Task<HttpClient> OfficerClientAsync(WebApplicationFactory<Program> app, HttpClient admin, Guid personId)
    {
        string email = $"officer-{personId:N}@test.local";
        var invite = await admin.PostAsJsonAsync("/api/admin/accounts", new InviteRequest(email, AccountRole.Officer, personId), Json);
        invite.EnsureSuccessStatusCode();
        string temp = (await invite.Content.ReadFromJsonAsync<InviteResult>(Json))!.TempPassword!;

        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = temp })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(temp, OfficerPasswordText)))
            .EnsureSuccessStatusCode();
        return client;
    }
}

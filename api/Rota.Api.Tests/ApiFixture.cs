using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Rota.Api.Auth;
using Testcontainers.PostgreSql;

namespace Rota.Api.Tests;

/// <summary>One Postgres container + app per test class; each test uses a fresh database.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminPasswordText = "test-admin-password";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task InitializeAsync() => await _db.StartAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    /// <summary>A new app instance against a new, migrated database.</summary>
    public WebApplicationFactory<Program> CreateApp()
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
                ["Admin:PasswordHash"] = AdminPassword.Hash(AdminPasswordText),
            }));
        });
    }

    public static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var res = await client.PostAsJsonAsync("/api/admin/login", new { password = AdminPasswordText });
        res.EnsureSuccessStatusCode();
        return client;
    }
}

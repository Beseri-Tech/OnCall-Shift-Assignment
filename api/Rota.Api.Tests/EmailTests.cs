using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>The email outbox: retries with backoff, gives up at the attempt cap, and respects the daily cap.</summary>
public class EmailTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<List<OutboxEmail>> OutboxAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RotaDbContext>().OutboxEmails.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
    }

    private static async Task<List<OutboxEmail>> WaitForAsync(WebApplicationFactory<Program> app, Func<List<OutboxEmail>, bool> done)
    {
        for (int i = 0; i < 100; i++)
        {
            var rows = await OutboxAsync(app);
            if (done(rows)) return rows;
            await Task.Delay(100);
        }
        return await OutboxAsync(app);
    }

    private static Task<HttpResponseMessage> ForgotAsync(WebApplicationFactory<Program> app) =>
        app.CreateClient().PostAsJsonAsync("/api/auth/forgot", new ForgotPasswordRequest(ApiFixture.AdminEmail));

    // Nothing listens on port 1, so every send fails like an unreachable mail server.
    private static readonly Dictionary<string, string?> DeadSmtp = new()
    {
        ["Smtp:Host"] = "127.0.0.1", ["Smtp:Port"] = "1", ["Smtp:EnableSsl"] = "false",
    };

    [Fact]
    public async Task A_failed_send_is_retried_later_without_blocking_the_request()
    {
        await using var app = fixture.CreateApp(DeadSmtp);
        (await ForgotAsync(app)).EnsureSuccessStatusCode();

        var email = (await WaitForAsync(app, r => r.Any(e => e.Attempts > 0))).Single();
        Assert.Equal((1, null, null), (email.Attempts, email.SentAt, email.FailedAt));
        Assert.NotNull(email.LastError);
        Assert.True(email.NextAttemptAt > DateTimeOffset.UtcNow.AddSeconds(20), "first retry waits about 30 seconds");
    }

    [Fact]
    public async Task Gives_up_after_the_attempt_cap()
    {
        await using var app = fixture.CreateApp(new(DeadSmtp) { ["Smtp:MaxAttempts"] = "1" });
        (await ForgotAsync(app)).EnsureSuccessStatusCode();

        var email = (await WaitForAsync(app, r => r.Any(e => e.FailedAt != null))).Single();
        Assert.Equal(1, email.Attempts);
        Assert.Null(email.SentAt);
        Assert.NotNull(email.FailedAt);
    }

    [Fact]
    public async Task A_blank_sender_falls_back_and_a_malformed_recipient_does_not_block_the_queue()
    {
        string mail = Path.Combine(Path.GetTempPath(), "rota-mail-" + Guid.NewGuid().ToString("N"));
        // Compose passes an unset ${SMTP_FROM} as an empty string.
        await using var app = fixture.CreateApp(new()
        {
            ["Smtp:PickupDirectory"] = mail, ["Smtp:From"] = "", ["Smtp:MaxPerMinute"] = "600",
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();
            db.OutboxEmails.Add(new OutboxEmail { To = "not an address", Subject = "Bad", Body = "x" });
            await db.SaveChangesAsync();
        }
        (await ForgotAsync(app)).EnsureSuccessStatusCode();

        var rows = await WaitForAsync(app, r => r.Count == 2 && r.All(e => e.SentAt != null || e.FailedAt != null));
        Assert.NotNull(rows.Single(e => e.Subject == "Bad").FailedAt);
        Assert.NotNull(rows.Single(e => e.Subject != "Bad").SentAt);
        Assert.Single(Directory.GetFiles(mail, "*.eml"));
        Directory.Delete(mail, recursive: true);
    }

    [Fact]
    public async Task The_daily_cap_holds_the_rest_back()
    {
        string mail = Path.Combine(Path.GetTempPath(), "rota-mail-" + Guid.NewGuid().ToString("N"));
        await using var app = fixture.CreateApp(new()
        {
            ["Smtp:PickupDirectory"] = mail, ["Smtp:MaxPerDay"] = "1", ["Smtp:MaxPerMinute"] = "600",
        });
        (await ForgotAsync(app)).EnsureSuccessStatusCode();
        (await ForgotAsync(app)).EnsureSuccessStatusCode();

        await WaitForAsync(app, r => r.Count(e => e.SentAt != null) == 1);
        await Task.Delay(1000);   // give the sender a chance to (wrongly) send the second one
        var rows = await OutboxAsync(app);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, e => e.SentAt != null);
        Assert.Single(rows, e => e.SentAt == null && e.FailedAt == null);
        Assert.Single(Directory.GetFiles(mail, "*.eml"));
        Directory.Delete(mail, recursive: true);
    }
}

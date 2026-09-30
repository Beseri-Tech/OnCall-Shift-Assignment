using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Services;

/// <summary>
/// Outgoing email, as a database outbox: requests add an <see cref="OutboxEmail"/> in the same SaveChanges as the change
/// it's about, and this background service sends them one at a time within the rate limits
/// (Gmail: ~500/day for a personal account). Temporary failures are retried with a capped backoff; the rest of the
/// queue keeps moving meanwhile. Transport: SMTP (Smtp:Host/Port/User/Password/From) or .eml files in
/// Smtp:PickupDirectory for development and tests.
/// </summary>
public sealed class Mailer(IConfiguration config, IHostEnvironment env, IServiceScopeFactory scopes, ILogger<Mailer> log) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(1);

    // Wakes the sender when something is queued, instead of waiting for the next poll.
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private string? Host => config["Smtp:Host"];
    private string? PickupDirectory => config["Smtp:PickupDirectory"];
    private int MaxPerMinute => Math.Max(1, config.GetValue("Smtp:MaxPerMinute", 20));
    private int MaxPerDay => Math.Max(1, config.GetValue("Smtp:MaxPerDay", 450));
    private int MaxAttempts => Math.Max(1, config.GetValue("Smtp:MaxAttempts", 6));

    public bool IsConfigured => !string.IsNullOrEmpty(Host) || !string.IsNullOrEmpty(PickupDirectory);

    /// <summary>
    /// Adds the email to <paramref name="db"/>; it goes out after the caller's SaveChanges. Returns false (and queues
    /// nothing) when email isn't configured, so callers can fall back, e.g. show the temporary password.
    /// </summary>
    public bool Enqueue(RotaDbContext db, string to, string subject, string body)
    {
        if (!IsConfigured)
        {
            log.LogWarning("Email to {To} not sent: SMTP is not configured.", to);
            if (env.IsDevelopment()) log.LogInformation("Subject: {Subject}\n{Body}", subject, body);
            return false;
        }
        db.OutboxEmails.Add(new OutboxEmail { To = to, Subject = subject, Body = body });
        db.SavedChanges += (_, _) => _wake.Writer.TryWrite(true);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsConfigured) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "Email sender failed; trying again shortly.");
            }

            // Sleep until something is queued or the next poll (retries and the daily cap come due by time).
            using var poll = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            poll.CancelAfter(PollInterval);
            try { await _wake.Reader.ReadAsync(poll.Token); }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
        }
    }

    // ponytail: one sender per database; running several app instances would need row locking (FOR UPDATE SKIP LOCKED).
    private async Task SendDueAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();
        var spacing = TimeSpan.FromMinutes(1) / MaxPerMinute;

        while (!ct.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            int sentToday = await db.OutboxEmails.CountAsync(e => e.SentAt > now.AddDays(-1), ct);
            if (sentToday >= MaxPerDay)
            {
                log.LogWarning("Daily email cap reached ({Cap} in 24 hours); the rest wait in the queue.", MaxPerDay);
                return;
            }

            var email = await db.OutboxEmails
                .Where(e => e.SentAt == null && e.FailedAt == null && e.NextAttemptAt <= now)
                .OrderBy(e => e.NextAttemptAt).ThenBy(e => e.Id)
                .FirstOrDefaultAsync(ct);
            if (email is null) return;

            await TrySendAsync(email, ct);
            await db.SaveChangesAsync(ct);
            await Task.Delay(spacing, ct);   // stay under Smtp:MaxPerMinute
        }
    }

    private async Task TrySendAsync(OutboxEmail email, CancellationToken ct)
    {
        email.Attempts++;
        try
        {
            using var message = new MailMessage(config["Smtp:From"] ?? config["Smtp:User"] ?? "rota@localhost", email.To,
                email.Subject, email.Body.ReplaceLineEndings("\r\n"));   // email wants CRLF; bare LF gets encoded as =0A
            using var client = CreateClient();
            await client.SendMailAsync(message, ct);
            email.SentAt = DateTimeOffset.UtcNow;
            email.LastError = null;
            log.LogInformation("Email \"{Subject}\" sent to {To}.", email.Subject, email.To);
        }
        catch (Exception e) when (e is SmtpException or IOException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            email.LastError = e.Message.Length > 500 ? e.Message[..500] : e.Message;

            // A 5xx for this recipient (no such mailbox, ...) won't get better; network trouble and 4xx "try later" might.
            bool permanent = e is SmtpFailedRecipientException r && (int)r.StatusCode >= 500;
            if (permanent || email.Attempts >= MaxAttempts)
            {
                email.FailedAt = DateTimeOffset.UtcNow;
                log.LogError(e, "Email \"{Subject}\" to {To} failed after {Attempts} attempt(s); giving up.", email.Subject, email.To, email.Attempts);
                return;
            }

            // 30s, 1m, 2m, 4m, ... capped at an hour.
            var delay = TimeSpan.FromTicks(Math.Min(MaxRetryDelay.Ticks, FirstRetry.Ticks << Math.Min(email.Attempts - 1, 20)));
            email.NextAttemptAt = DateTimeOffset.UtcNow + delay;
            log.LogWarning(e, "Email to {To} failed (attempt {Attempt}/{Max}); retrying in {Delay}.", email.To, email.Attempts, MaxAttempts, delay);
        }
    }

    private SmtpClient CreateClient()
    {
        if (!string.IsNullOrEmpty(PickupDirectory))
        {
            string dir = Path.GetFullPath(PickupDirectory, env.ContentRootPath);
            Directory.CreateDirectory(dir);
            return new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = dir };
        }

        return new SmtpClient(Host, config.GetValue("Smtp:Port", 587))
        {
            EnableSsl = config.GetValue("Smtp:EnableSsl", true),
            Timeout = 30_000,
            Credentials = string.IsNullOrEmpty(config["Smtp:User"])
                ? null
                : new NetworkCredential(config["Smtp:User"], config["Smtp:Password"]),
        };
    }

    /// <summary>Links in emails: App:BaseUrl, else the address the request came in on.</summary>
    public string BaseUrl(HttpContext http) =>
        (config["App:BaseUrl"] is { Length: > 0 } url ? url : $"{http.Request.Scheme}://{http.Request.Host}").TrimEnd('/');
}

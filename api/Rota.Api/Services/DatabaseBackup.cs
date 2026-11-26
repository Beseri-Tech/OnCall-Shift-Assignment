using System.Diagnostics;
using System.Globalization;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Npgsql;
using Rota.Api.Storage;

namespace Rota.Api.Services;

/// <summary>
/// Nightly <c>pg_dump</c> of the database, uploaded to a separate private R2 bucket (config section "Backup:R2").
/// Off unless that bucket is fully configured. A bucket lifecycle rule (set in Cloudflare) expires old dumps.
/// </summary>
public sealed class DatabaseBackup(IConfiguration config, LocalClock clock, TimeProvider time, ILogger<DatabaseBackup> log) : BackgroundService
{
    private static readonly TimeSpan DumpTimeout = TimeSpan.FromMinutes(30);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>The next moment, as UTC, that it is <paramref name="hour"/>:00 in <paramref name="zone"/>: today's if still ahead, else tomorrow's.</summary>
    public static DateTimeOffset NextRun(DateTimeOffset nowUtc, TimeZoneInfo zone, int hour)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);
        var next = At(today, zone, hour);
        return next > nowUtc ? next : At(today.AddDays(1), zone, hour);
    }

    private static DateTimeOffset At(DateOnly day, TimeZoneInfo zone, int hour)
    {
        var local = day.ToDateTime(new TimeOnly(hour, 0));
        if (zone.IsInvalidTime(local)) local = local.AddHours(1); // skipped by a clock change
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var r2 = new R2Options();
        config.GetSection("Backup:R2").Bind(r2);
        if (!config.GetValue("Backup:Enabled", true) || !r2.IsConfigured) return;

        var hour = config.GetValue("Backup:Hour", 3);
        if (hour is < 0 or > 23)
        {
            log.LogWarning("Backup:Hour {Hour} is not between 0 and 23; using 3.", hour);
            hour = 3;
        }
        var prefix = (config["Backup:Prefix"] ?? "production").Trim('/');
        if (prefix.Length == 0) prefix = "production";

        using var s3 = R2DocumentStorage.CreateClient(r2);
        log.LogInformation("Database backup is on: every day at {Hour:00}:00 to bucket {Bucket}/{Prefix}.", hour, r2.BucketName, prefix);

        try
        {
            if (config.GetValue("Backup:RunOnStartup", false))
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await RunOnceAsync(s3, r2.BucketName, prefix, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var wait = NextRun(time.GetUtcNow(), clock.Zone, hour) - time.GetUtcNow();
                if (wait > TimeSpan.Zero) await Task.Delay(wait, stoppingToken);
                await RunOnceAsync(s3, r2.BucketName, prefix, stoppingToken);
                // Never run twice in the same minute, even if the timer fires a little early.
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunOnceAsync(IAmazonS3 s3, string bucket, string prefix, CancellationToken ct)
    {
        string? file = null;
        try
        {
            var stamp = TimeZoneInfo.ConvertTime(time.GetUtcNow(), clock.Zone).ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture);
            file = Path.Combine(Path.GetTempPath(), $"rota-{stamp}.dump");
            var key = $"{prefix}/rota-{stamp}.dump";

            await DumpAsync(file, ct);
            var size = new FileInfo(file).Length;
            if (size == 0) throw new InvalidOperationException("pg_dump produced an empty file.");

            // One PutObject (dumps are far below its 5 GiB limit). R2 rejects the SDK's chunked payload signing
            // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD), so send the body in one piece with an unsigned payload over HTTPS.
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                FilePath = file,
                UseChunkEncoding = false,
                DisablePayloadSigning = true,
            }, ct);
            var stored = await s3.GetObjectMetadataAsync(bucket, key, ct);
            if (stored.ContentLength != size)
                throw new InvalidOperationException($"Uploaded {stored.ContentLength} bytes but the dump is {size} bytes.");

            log.LogInformation("Database backup uploaded: {Key} ({Size:N0} bytes).", key, size);
            await PingAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError(e, "Database backup failed; trying again tomorrow night.");
        }
        finally
        {
            if (file is not null)
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }
    }

    private async Task DumpAsync(string file, CancellationToken ct)
    {
        var cs = new NpgsqlConnectionStringBuilder(config.GetConnectionString("Rota"));
        var psi = new ProcessStartInfo("pg_dump")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[]
        {
            "-Fc", "--no-owner", "--no-privileges", "-w",
            "-h", cs.Host ?? "localhost", "-p", cs.Port.ToString(CultureInfo.InvariantCulture),
            "-U", cs.Username ?? "", "-d", cs.Database ?? "", "-f", file,
        })
            psi.ArgumentList.Add(a);
        // The password goes through the environment only, never the command line.
        psi.Environment["PGPASSWORD"] = cs.Password ?? "";
        psi.Environment["PGCONNECT_TIMEOUT"] = "30";
        var sslMode = cs.SslMode switch
        {
            SslMode.Disable => "disable",
            SslMode.Allow => "allow",
            SslMode.Prefer => "prefer",
            SslMode.Require => "require",
            SslMode.VerifyCA => "verify-ca",
            SslMode.VerifyFull => "verify-full",
            _ => null,
        };
        if (sslMode is not null) psi.Environment["PGSSLMODE"] = sslMode;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(DumpTimeout);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start pg_dump.");
        var stderr = new StringBuilder();
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (stderr) stderr.AppendLine(e.Data); };
        p.OutputDataReceived += (_, _) => { };
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        try
        {
            await p.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            throw new TimeoutException($"pg_dump did not finish within {DumpTimeout.TotalMinutes:0} minutes.");
        }
        p.WaitForExit(); // flush the redirected streams
        if (p.ExitCode != 0)
        {
            string err;
            lock (stderr) err = stderr.ToString().Trim();
            throw new InvalidOperationException($"pg_dump exited with code {p.ExitCode}: {err}");
        }
    }

    /// <summary>Optional dead-man's switch: a healthchecks-style URL called after each good backup, so silence means trouble.</summary>
    private async Task PingAsync(CancellationToken ct)
    {
        var url = config["Backup:PingUrl"];
        if (string.IsNullOrWhiteSpace(url)) return;
        try { (await Http.GetAsync(url, ct)).Dispose(); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("Backup ping failed ({Reason}).", e.GetType().Name); // never log the URL
        }
    }
}

using Rota.Api.Storage;

namespace Rota.Api.Services;

/// <summary>Runs <see cref="DocumentService.CleanupAsync"/> shortly after start and then every few hours.</summary>
public sealed class DocumentCleanup(IServiceScopeFactory scopes, IConfiguration config, ILogger<DocumentCleanup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("R2:CleanupEnabled", true)) return;
        var every = TimeSpan.FromHours(Math.Max(1, config.GetValue("R2:CleanupEveryHours", 6)));
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                int removed = await scope.ServiceProvider.GetRequiredService<DocumentService>().CleanupAsync(stoppingToken);
                if (removed > 0) log.LogInformation("Document cleanup removed {Count} document(s).", removed);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "Document cleanup failed; trying again later.");
            }
            try { await Task.Delay(every, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}

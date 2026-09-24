using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public sealed class ImdbImportJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ImdbImportOptions> options,
    CatalogBootstrapOptions bootstrap,
    CatalogScheduleGate schedules,
    TimeProvider clock,
    ILogger<ImdbImportJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var config = options.Value;
        if (bootstrap.Provider == "imdb")
        {
            var succeeded = false;
            try { succeeded = await RunInvocationAsync(config, "bootstrap", ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception error)
            {
                logger.LogError("IMDb bootstrap stopped: {FailureCategory}; both catalog schedules remain paused", error.GetType().Name);
            }
            finally { schedules.FinishBootstrap("imdb", succeeded); }
        }

        if (!config.Enabled || !await schedules.WaitForSchedulesAsync(ct)) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = clock.GetUtcNow();
                var next = CatalogScheduleGate.NextDailyRun(now, config.ScheduleHourUtc);
                logger.LogInformation("Next IMDb catalog import: {NextRunUtc}", next);
                await Task.Delay(next - now, clock, ct);
                await RunInvocationAsync(config, "scheduled", ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (InvalidDataException)
            {
                logger.LogError("IMDb feed blocked; its schedule is paused until operator remediation and restart");
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning("Scheduled IMDb invocation stopped: {FailureCategory}; next attempt is the next daily schedule", error.GetType().Name);
            }
        }
    }

    private async Task<bool> RunInvocationAsync(ImdbImportOptions config, string mode, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(config.MaxWholeSessionSeconds));
        await using var scope = scopeFactory.CreateAsyncScope();
        logger.LogInformation("IMDb {Mode} {SessionId} started: HTTP cap {HttpLimit}, duration cap {Seconds}s, compressed bytes/feed {CompressedLimit}, decompressed bytes/feed {DecompressedLimit}, temporary bytes {DiskLimit}",
            mode, id, config.MaxHttpAttempts, config.MaxWholeSessionSeconds, config.MaxCompressedBytesPerFeed,
            config.MaxDecompressedBytesPerFeed, config.MaxTemporaryDiskBytes);
        try
        {
            var imported = await scope.ServiceProvider.GetRequiredService<ImdbImportService>().ImportAsync(deadline.Token);
            if (!imported.Completed) return false;
            var loaded = await scope.ServiceProvider.GetRequiredService<ImdbLoadService>().LoadAsync(deadline.Token);
            logger.LogInformation("IMDb {Mode} {SessionId} completed: basics {Basics}, episodes {Episodes}, ratings {Ratings}, domain rows affected {Affected}",
                mode, id, imported.Basics, imported.Episodes, imported.Ratings, loaded.Affected);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            logger.LogInformation("IMDb {Mode} {SessionId} stopped at the whole-invocation deadline; a new invocation requires full feed replay", mode, id);
            return false;
        }
    }
}

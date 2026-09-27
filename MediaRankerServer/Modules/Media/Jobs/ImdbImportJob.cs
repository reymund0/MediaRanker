using MediaRankerServer.Modules.Media.Data;
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
        var execution = new ImdbImportExecution(config);
        execution.SetStage("scope-resolution");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(config.MaxWholeSessionSeconds));
        logger.LogInformation("IMDb {Mode} {SessionId} started: HTTP cap {HttpLimit}, duration cap {Seconds}s, compressed bytes/feed {CompressedLimit}, decompressed bytes/feed {DecompressedLimit}, temporary bytes {DiskLimit}",
            mode, id, config.MaxHttpAttempts, config.MaxWholeSessionSeconds, config.MaxCompressedBytesPerFeed,
            config.MaxDecompressedBytesPerFeed, config.MaxTemporaryDiskBytes);
        var outcome = "failed";
        var failureCategory = "none";
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var imported = await scope.ServiceProvider.GetRequiredService<ImdbImportService>().ImportAsync(execution, deadline.Token);
            if (!imported.Completed)
            {
                outcome = "incomplete";
                return false;
            }
            var loaded = await scope.ServiceProvider.GetRequiredService<ImdbLoadService>().LoadAsync(execution, deadline.Token);
            logger.LogInformation("IMDb {Mode} {SessionId} completed: basics {Basics}, episodes {Episodes}, ratings {Ratings}, domain rows affected {Affected}",
                mode, id, imported.Basics, imported.Episodes, imported.Ratings, loaded.Affected);
            outcome = "completed";
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            outcome = "deadline";
            failureCategory = "whole-session-deadline";
            return false;
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            failureCategory = "host-cancellation";
            throw;
        }
        catch (ImdbBudgetExceededException error)
        {
            outcome = "budget-stop";
            failureCategory = BudgetFailureCategory(error);
            throw;
        }
        catch (Exception error)
        {
            failureCategory = error.GetType().Name;
            throw;
        }
        finally
        {
            var counters = execution.Counters;
            logger.LogInformation("IMDb {Mode} {SessionId} {Outcome} at stage {Stage}; failure category {FailureCategory}; HTTP attempts {HttpAttempts}; elapsed {ElapsedMs}ms; rows read {RowsRead}; parsed {ParsedRows}; filtered {FilteredRows}; batches committed {BatchesCommitted}; rows affected {RowsAffected}; rows skipped {RowsSkipped}; cleanup affected {CleanupRowsAffected}; load affected {LoadRowsAffected}.",
                mode, id, outcome, execution.CurrentStage, failureCategory, execution.HttpAttempts,
                execution.Elapsed.TotalMilliseconds, counters.RowsRead, counters.ParsedRows, counters.FilteredRows,
                counters.BatchesCommitted, counters.RowsAffected, counters.RowsSkipped,
                counters.CleanupRowsAffected, counters.LoadRowsAffected);
        }
    }

    private static string BudgetFailureCategory(ImdbBudgetExceededException error) => error.Message switch
    {
        "IMDb HTTP attempt allowance exhausted." => "http-attempt-limit",
        "IMDb compressed or temporary-disk allowance exhausted." => "compressed-or-temporary-disk-limit",
        "IMDb compressed feed allowance exhausted." => "compressed-byte-limit",
        "IMDb decompressed-byte allowance exhausted." => "decompressed-byte-limit",
        "IMDb TSV line character allowance exhausted." => "line-character-limit",
        "IMDb feed row allowance exhausted." => "feed-row-limit",
        _ => "other-budget-limit"
    };
}

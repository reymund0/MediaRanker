using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public sealed class IgdbImportJob(
    IServiceScopeFactory scopeFactory,
    IOptions<IgdbOptions> options,
    CatalogBootstrapOptions bootstrap,
    CatalogScheduleGate schedules,
    TimeProvider clock,
    ILogger<IgdbImportJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var config = options.Value;
        if (bootstrap.Provider == "igdb")
        {
            var succeeded = false;
            try { succeeded = await RunBootstrapAsync(config, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception error)
            {
                logger.LogError("IGDB bootstrap stopped: {FailureCategory}; both catalog schedules remain paused", error.GetType().Name);
            }
            finally
            {
                schedules.FinishBootstrap("igdb", succeeded);
                if (!succeeded)
                    logger.LogWarning("IGDB bootstrap paused; both catalog schedules, including release-day admission, remain paused for this process");
            }
        }

        if (!config.ImportEnabled || !await schedules.WaitForSchedulesAsync(ct)) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = clock.GetUtcNow();
                var next = CatalogScheduleGate.NextDailyRun(now, config.ScheduleHourUtc);
                logger.LogInformation("Next IGDB catalog import: {NextRunUtc}", next);
                await Task.Delay(next - now, clock, ct);
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IgdbImportService>().ImportAsync(ct);
                LogProgress("scheduled", Guid.NewGuid(), result);
                if (result.StopReason == ImportStopReason.AdmissionBlocked)
                {
                    logger.LogError("IGDB admission blocked; this catalog's schedule is paused until operator remediation and restart");
                    return;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogWarning("Scheduled IGDB invocation stopped: {FailureCategory}; next attempt is the next daily schedule", error.GetType().Name);
            }
        }
    }

    private async Task<bool> RunBootstrapAsync(IgdbOptions config, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        using var session = new ImportBudgetSession(new(bootstrap.MaxHttpAttempts, bootstrap.MaxAdmissionRows,
            TimeSpan.FromSeconds(bootstrap.MaxSeconds)), clock);
        using var deadline = session.CreateLinkedTokenSource(ct);
        logger.LogInformation("IGDB bootstrap {SessionId} started: HTTP cap {HttpLimit}, admission cap {RowLimit}, duration cap {Seconds}s",
            id, bootstrap.MaxHttpAttempts, bootstrap.MaxAdmissionRows, bootstrap.MaxSeconds);
        try
        {
            while (session.Remaining > TimeSpan.Zero)
            {
                using var unit = session.CreateUnit(new(config.WorkUnitPageLimit, config.WorkUnitHttpAttemptLimit,
                    config.WorkUnitAdmissionRowLimit, config.WorkUnitAdmissionBatchLimit, TimeSpan.FromSeconds(config.WorkUnitSeconds)));
                IgdbImportRunResult result;
                await using (var scope = scopeFactory.CreateAsyncScope())
                    result = await scope.ServiceProvider.GetRequiredService<IgdbImportService>()
                        .ImportAsync(unit, bootstrap: true, deadline.Token);
                LogProgress("bootstrap", id, result);
                if (result.CatalogReady) return true;
                if (result.StopReason is not (ImportStopReason.UnitPageLimit or ImportStopReason.UnitHttpLimit
                    or ImportStopReason.UnitAdmissionLimit or ImportStopReason.UnitDeadline)) return false;
                await Task.Delay(TimeSpan.FromMilliseconds(bootstrap.IgdbYieldMilliseconds), clock, deadline.Token);
            }
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            logger.LogInformation("IGDB bootstrap {SessionId} reached its session deadline; both catalog schedules are paused", id);
            return false;
        }
        finally
        {
            logger.LogInformation("IGDB bootstrap {SessionId} totals: {HttpAttempts} HTTP attempts, {AdmissionRows} admission rows reserved, {ElapsedMs}ms elapsed, operations {@HttpOperations}",
                id, session.HttpAttempts, session.AdmissionRows, session.Elapsed.TotalMilliseconds, session.HttpAttemptsByOperation);
        }
    }

    private void LogProgress(string mode, Guid id, IgdbImportRunResult result) => logger.LogInformation(
        "IGDB {Mode} {SessionId}: stop {StopReason}, lease acquired {LeaseAcquired}, pages {Pages}, staged {Rows}, admitted {Admitted}, upstream complete {UpstreamComplete}, admission remaining {AdmissionRemaining}, ready {CatalogReady}, incremental pending {IncrementalPending}, lease busy until {LeaseBusyUntil}, retry after {RetryAfter}, cursor {Cursor}, maximum ID {MaximumId}, admission retries {AdmissionRetries}, failing batch {FailingBatch}",
        mode, id, result.StopReason, result.LeaseAcquired, result.PagesCommitted, result.RowsCommitted,
        result.EligibleGamesLoaded, result.RunCompleted, result.AdmissionRemaining, result.CatalogReady, result.IncrementalPending,
        result.LeaseBusyUntil, result.RetryAfter, result.DurableCursor, result.RunMaximumId,
        result.AdmissionRetryCount, result.FailingBatch);
}

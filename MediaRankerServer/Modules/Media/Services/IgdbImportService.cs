using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Modules.Media.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Services;

public sealed record IgdbImportRunResult(int PagesCommitted, int RowsCommitted, int EligibleGamesLoaded,
    bool RunCompleted, bool LeaseAcquired)
{
    public ImportStopReason StopReason { get; init; }
    public int? AdmissionRemaining { get; init; }
    public bool CatalogReady { get; init; }
    public bool IncrementalPending { get; init; }
    public DateTimeOffset? LeaseBusyUntil { get; init; }
    public int HttpAttempts { get; init; }
    public int AdmissionRows { get; init; }
    public int AdmissionBatches { get; init; }
    public long? DurableCursor { get; init; }
    public long? RunMaximumId { get; init; }
    public int AdmissionRetryCount { get; init; }
    public string? FailingBatch { get; init; }
    public TimeSpan Elapsed { get; init; }
    public IReadOnlyDictionary<string, int> HttpAttemptsByOperation { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public TimeSpan? RetryAfter { get; init; }
}

public sealed class IgdbImportService(
    IIgdbClient igdbClient,
    IIgdbImportProvider importProvider,
    IOptions<IgdbOptions> options,
    IOptions<ArtworkOptions> artworkOptions,
    ILogger<IgdbImportService> logger,
    IServiceScopeFactory? scopeFactory = null)
{
    // Let concurrent artwork requests run between bounded scheduled import units.
    private static readonly TimeSpan ScheduledWorkUnitYield = TimeSpan.FromSeconds(1);
    private static readonly IReadOnlySet<string> SupportedGameTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Main game", "Remake", "Remaster"
    };

    private readonly IgdbOptions config = options.Value;
    private readonly ArtworkOptions artworkConfig = artworkOptions.Value;

    public async Task<IgdbImportRunResult> ImportAsync(CancellationToken ct = default)
    {
        using var session = new ImportBudgetSession(new ImportSessionLimits(
            config.ScheduledSessionHttpAttemptLimit,
            config.ScheduledSessionAdmissionRowLimit,
            TimeSpan.FromMinutes(config.ScheduledSessionMinutes)));
        using var sessionCancellation = session.CreateLinkedTokenSource(ct);

        var pagesCommitted = 0;
        var rowsCommitted = 0;
        var eligibleGamesLoaded = 0;
        var admissionRetryCount = 0;
        var pagesReserved = 0;
        var leaseAcquired = false;
        IgdbImportRunResult? lastResult = null;

        while (true)
        {
            var unitPageLimit = Math.Min(config.WorkUnitPageLimit, config.PageBudget - pagesReserved);
            using var unit = session.CreateUnit(new ImportWorkUnitLimits(
                unitPageLimit,
                config.WorkUnitHttpAttemptLimit,
                config.WorkUnitAdmissionRowLimit,
                config.WorkUnitAdmissionBatchLimit,
                TimeSpan.FromSeconds(config.WorkUnitSeconds)));

            var hasPreviousResult = lastResult is not null;
            var previousCursor = lastResult?.DurableCursor;
            var previousAdmissionRows = session.AdmissionRows;
            var previousAdmissionBatches = session.AdmissionBatches;
            IgdbImportRunResult unitResult;
            if (scopeFactory is null)
            {
                // Direct callers and unit tests may construct this service without a
                // scope factory. Keep the same session/unit limits while reusing the
                // supplied provider for each work unit.
                unitResult = await ImportAsync(unit, bootstrap: false, sessionCancellation.Token);
            }
            else
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                unitResult = await scope.ServiceProvider.GetRequiredService<IgdbImportService>()
                    .ImportAsync(unit, bootstrap: false, sessionCancellation.Token);
            }

            pagesCommitted += unitResult.PagesCommitted;
            pagesReserved += unit.Pages;
            rowsCommitted += unitResult.RowsCommitted;
            eligibleGamesLoaded += unitResult.EligibleGamesLoaded;
            admissionRetryCount += unitResult.AdmissionRetryCount;
            leaseAcquired |= unitResult.LeaseAcquired;
            lastResult = AggregateScheduledResult(unitResult, session, pagesCommitted, rowsCommitted,
                eligibleGamesLoaded, admissionRetryCount, leaseAcquired);

            if (unitResult.RunCompleted || !CanContinueScheduledRun(unitResult.StopReason))
                return lastResult;

            if (sessionCancellation.IsCancellationRequested)
            {
                var stop = session.IsExpired() ? ImportStopReason.SessionDeadline : ImportStopReason.Canceled;
                session.SetStopReason(stop);
                return AggregateScheduledResult(lastResult, session, pagesCommitted, rowsCommitted,
                    eligibleGamesLoaded, admissionRetryCount, leaseAcquired) with { StopReason = stop };
            }

            if (pagesReserved >= config.PageBudget)
                return lastResult with { StopReason = ImportStopReason.UnitPageLimit };

            var sessionStop = GetSessionStopReason(session);
            if (sessionStop != ImportStopReason.None)
            {
                session.SetStopReason(sessionStop);
                return lastResult with { StopReason = sessionStop };
            }

            var unitMadeProgress = unitResult.PagesCommitted > 0
                || unitResult.RowsCommitted > 0
                || unitResult.EligibleGamesLoaded > 0
                || (hasPreviousResult && unitResult.DurableCursor != previousCursor)
                || session.AdmissionRows > previousAdmissionRows
                || session.AdmissionBatches > previousAdmissionBatches;
            if (!unitMadeProgress)
                return lastResult;

            try
            {
                await Task.Delay(ScheduledWorkUnitYield, sessionCancellation.Token);
            }
            catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
            {
                var stop = session.IsExpired() ? ImportStopReason.SessionDeadline : ImportStopReason.Canceled;
                session.SetStopReason(stop);
                return AggregateScheduledResult(lastResult, session, pagesCommitted, rowsCommitted,
                    eligibleGamesLoaded, admissionRetryCount, leaseAcquired) with { StopReason = stop };
            }
        }
    }

    private static IgdbImportRunResult AggregateScheduledResult(IgdbImportRunResult result,
        ImportBudgetSession session, int pagesCommitted, int rowsCommitted, int eligibleGamesLoaded,
        int admissionRetryCount, bool leaseAcquired) => result with
    {
        PagesCommitted = pagesCommitted,
        RowsCommitted = rowsCommitted,
        EligibleGamesLoaded = eligibleGamesLoaded,
        LeaseAcquired = leaseAcquired,
        AdmissionRetryCount = admissionRetryCount,
        HttpAttempts = session.HttpAttempts,
        AdmissionRows = session.AdmissionRows,
        AdmissionBatches = session.AdmissionBatches,
        Elapsed = session.Elapsed,
        HttpAttemptsByOperation = session.HttpAttemptsByOperation
    };

    private static bool CanContinueScheduledRun(ImportStopReason stopReason) => stopReason is
        ImportStopReason.UnitPageLimit or ImportStopReason.UnitHttpLimit or ImportStopReason.UnitAdmissionLimit
        or ImportStopReason.UnitDeadline;

    private static ImportStopReason GetSessionStopReason(ImportBudgetSession session)
    {
        if (session.Remaining <= TimeSpan.Zero)
            return ImportStopReason.SessionDeadline;
        if (session.HttpAttempts >= session.Limits.MaxHttpAttempts)
            return ImportStopReason.SessionHttpLimit;
        if (session.AdmissionRows >= session.Limits.MaxAdmissionRows)
            return ImportStopReason.SessionAdmissionLimit;
        return ImportStopReason.None;
    }

    public Task<IgdbImportRunResult> ImportAsync(ImportWorkUnitBudget budget, bool bootstrap,
        CancellationToken ct = default) => ImportAsyncCore(budget, bootstrap, ct, includeProgress: true);

    private async Task<IgdbImportRunResult> ImportAsyncCore(ImportWorkUnitBudget budget, bool bootstrap,
        CancellationToken cancellationToken, bool includeProgress)
    {
        if (!config.ImportEnabled)
        {
            logger.LogInformation("IGDB catalog import is disabled.");
            return CreateResult(0, 0, 0, false, false, budget, includeProgress, ImportStopReason.Disabled);
        }

        using var linked = budget.CreateLinkedTokenSource(cancellationToken);
        var ct = linked.Token;
        IgdbImportLease? lease = null;
        var pagesCommitted = 0;
        var rowsCommitted = 0;
        var upstreamCompleted = false;
        IgdbAdmissionResult admission = new(0, 0, null, false, false);

        try
        {
            lease = await importProvider.TryAcquireLeaseAsync(DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(config.LeaseSeconds), ct);
            if (lease is null)
            {
                var busyUntil = await importProvider.GetLeaseBusyUntilAsync(ct);
                return CreateResult(0, 0, 0, false, false, budget, includeProgress,
                    ImportStopReason.LeaseBusy, busyUntil: busyUntil);
            }

            var state = lease.State;
            // Admission is local and bounded. Drain/probe it before spending an
            // upstream request, including when bootstrap is launched with an old
            // incremental window still persisted.
            admission = await importProvider.LoadEligibleGamesAsync(lease, SupportedGameTypeNames,
                DateTimeOffset.UtcNow,
                TimeSpan.FromDays(artworkConfig.PositiveCacheDays),
                TimeSpan.FromDays(artworkConfig.NegativeCacheDays), budget, ct);
            if (admission.Blocked)
            {
                budget.SetStopReason(ImportStopReason.AdmissionBlocked);
                return CreateResult(0, 0, admission.LoadedRows, false, true, budget, includeProgress,
                    ImportStopReason.AdmissionBlocked, admission.RemainingRows,
                    admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                    durableCursor: state.LastCommittedId, runMaximumId: state.RunMaximumId);
            }
            if (!admission.Completed && admission.RemainingRows.GetValueOrDefault() > 0)
            {
                if (budget.StopReason == ImportStopReason.None)
                    budget.SetStopReason(ImportStopReason.UnitAdmissionLimit);
                return CreateResult(0, 0, admission.LoadedRows, false, true, budget, includeProgress,
                    budget.StopReason, admission.RemainingRows,
                    admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                    durableCursor: state.LastCommittedId, runMaximumId: state.RunMaximumId);
            }

            var admissionOnly = bootstrap && state.BootstrapCompleted;
            if (!admissionOnly)
            {
                var beforeDiscovery = FetchStopReason(budget);
                if (beforeDiscovery != ImportStopReason.None)
                {
                    budget.SetStopReason(beforeDiscovery);
                    return CreateResult(0, 0, admission.LoadedRows, false, true, budget, includeProgress,
                        beforeDiscovery, admission.RemainingRows,
                        admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                        durableCursor: state.LastCommittedId, runMaximumId: state.RunMaximumId);
                }
                var gameTypes = await igdbClient.GetGameTypesAsync(budget, ct);
                var gameTypeNames = gameTypes.ToDictionary(x => x.Id, x => x.Name);

                if (!HasActiveRun(state))
                {
                    if (!state.BootstrapCompleted)
                    {
                        var maximumId = await igdbClient.GetMaximumGameIdAsync(budget, ct);
                        state = await importProvider.StartRunAsync(lease, true, maximumId, null, null,
                            DateTimeOffset.UtcNow, ct);
                    }
                    else
                    {
                        var completedWatermark = state.LastCompletedUpdatedAt ?? state.BootstrapStartedAt ?? DateTimeOffset.UtcNow;
                        var lowerBound = completedWatermark.AddMinutes(-config.IncrementalOverlapMinutes);
                        state = await importProvider.StartRunAsync(lease, false, null, lowerBound,
                            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ct);
                    }
                }

                while (true)
                {
                    var beforePage = FetchStopReason(budget);
                    if (beforePage != ImportStopReason.None)
                    {
                        budget.SetStopReason(beforePage);
                        break;
                    }
                    var page = await igdbClient.GetGamesAsync(new IgdbGameQuery(
                        state.LastCommittedId,
                        state.RunMaximumId,
                        state.RunUpdatedAfter,
                        state.RunUpdatedBefore,
                        config.PageSize), budget, ct);

                    if (page.Count == 0)
                    {
                        await importProvider.CompleteRunAsync(lease, DateTimeOffset.UtcNow, ct);
                        upstreamCompleted = true;
                        break;
                    }

                    var previousId = state.LastCommittedId;
                    var pageIsIncreasing = true;
                    foreach (var game in page)
                    {
                        if (game.Id <= previousId)
                        {
                            pageIsIncreasing = false;
                            break;
                        }
                        previousId = game.Id;
                    }
                    if (!pageIsIncreasing)
                    {
                        budget.SetStopReason(ImportStopReason.NonIncreasingPage);
                        break;
                    }

                    var fetchedAt = DateTimeOffset.UtcNow;
                    var staging = page.Select(game => new IgdbImport
                    {
                        IgdbGameId = game.Id,
                        Name = game.Name?.Trim(),
                        FirstReleaseDate = game.FirstReleaseDate,
                        GameTypeId = game.GameTypeId,
                        GameTypeName = game.GameTypeId is { } typeId && gameTypeNames.TryGetValue(typeId, out var typeName) ? typeName : null,
                        VersionParentId = game.VersionParentId,
                        CoverImageId = IsValidCoverImageId(game.CoverImageId) ? game.CoverImageId : null,
                        ProviderUpdatedAt = game.UpdatedAt,
                        FetchedAt = fetchedAt,
                        CreatedAt = fetchedAt,
                        UpdatedAt = fetchedAt
                    }).ToArray();

                    state = await importProvider.CommitPageAsync(lease, staging, page[^1].Id, ct);
                    pagesCommitted++;
                    rowsCommitted += staging.Length;

                    var pageAdmission = await importProvider.LoadEligibleGamesAsync(lease, SupportedGameTypeNames,
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromDays(artworkConfig.PositiveCacheDays),
                        TimeSpan.FromDays(artworkConfig.NegativeCacheDays), budget, ct);
                    admission = MergeAdmission(admission, pageAdmission);
                    if (admission.Blocked)
                    {
                        budget.SetStopReason(ImportStopReason.AdmissionBlocked);
                        break;
                    }
                    if (!admission.Completed)
                    {
                        if (budget.StopReason == ImportStopReason.None)
                            budget.SetStopReason(ImportStopReason.UnitAdmissionLimit);
                        break;
                    }

                    if (page.Count < config.PageSize)
                    {
                        await importProvider.CompleteRunAsync(lease, DateTimeOffset.UtcNow, ct);
                        upstreamCompleted = true;
                        break;
                    }
                }

                if (!upstreamCompleted && budget.StopReason == ImportStopReason.None)
                    budget.SetStopReason(ImportStopReason.UnitPageLimit);
            }
            else
            {
                upstreamCompleted = true;
            }

            var ready = lease.State.BootstrapCompleted && admission.Completed && admission.RemainingRows == 0;
            if (upstreamCompleted && admission.Completed && budget.StopReason == ImportStopReason.None)
                budget.SetStopReason(ImportStopReason.Completed);
            return CreateResult(pagesCommitted, rowsCommitted, admission.LoadedRows, upstreamCompleted, true,
                budget, includeProgress, budget.StopReason, admission.RemainingRows, ready,
                IncrementalPending: HasActiveRun(lease.State) && !lease.State.RunIsBootstrap,
                admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                durableCursor: lease.State.LastCommittedId, runMaximumId: lease.State.RunMaximumId);
        }
        catch (ImportBudgetExceededException ex)
        {
            budget.SetStopReason(ex.Reason);
            return CreateResult(pagesCommitted, rowsCommitted, admission.LoadedRows, upstreamCompleted, lease is not null,
                budget, includeProgress, ex.Reason, admission.RemainingRows,
                admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                durableCursor: lease?.State.LastCommittedId, runMaximumId: lease?.State.RunMaximumId);
        }
        catch (ProviderRequestException ex)
        {
            var reason = ex.IsLocalCooldown ? ImportStopReason.ProviderCooldown : ImportStopReason.ProviderFailure;
            budget.SetStopReason(reason);
            return CreateResult(pagesCommitted, rowsCommitted, admission.LoadedRows, upstreamCompleted, lease is not null,
                budget, includeProgress, reason, admission.RemainingRows, retryAfter: ex.RetryAfter,
                admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                durableCursor: lease?.State.LastCommittedId, runMaximumId: lease?.State.RunMaximumId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || ct.IsCancellationRequested)
        {
            var reason = budget.StopReason;
            if (reason == ImportStopReason.None)
            {
                reason = budget.Session.IsExpired()
                    ? ImportStopReason.SessionDeadline
                    : budget.Elapsed >= budget.Limits.MaxDuration
                        ? ImportStopReason.UnitDeadline
                        : ImportStopReason.Canceled;
                budget.SetStopReason(reason);
            }
            return CreateResult(pagesCommitted, rowsCommitted, admission.LoadedRows, upstreamCompleted, lease is not null,
                budget, includeProgress, reason, admission.RemainingRows,
                admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                durableCursor: lease?.State.LastCommittedId, runMaximumId: lease?.State.RunMaximumId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("lease", StringComparison.OrdinalIgnoreCase))
        {
            budget.SetStopReason(ImportStopReason.LeaseLost);
            return CreateResult(pagesCommitted, rowsCommitted, admission.LoadedRows, upstreamCompleted, lease is not null,
                budget, includeProgress, ImportStopReason.LeaseLost, admission.RemainingRows,
                admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                durableCursor: lease?.State.LastCommittedId, runMaximumId: lease?.State.RunMaximumId);
        }
        catch (Exception ex)
        {
            logger.LogError("IGDB import stopped; category {Category}; pages {Pages}; rows {Rows}.",
                ex.GetType().Name, pagesCommitted, rowsCommitted);
            budget.SetStopReason(ImportStopReason.ProviderFailure);
            return CreateResult(pagesCommitted, rowsCommitted, admission.LoadedRows, upstreamCompleted, lease is not null,
                budget, includeProgress, ImportStopReason.ProviderFailure, admission.RemainingRows,
                admissionRetryCount: admission.RetryCount, failingBatch: admission.BlockBatch,
                durableCursor: lease?.State.LastCommittedId, runMaximumId: lease?.State.RunMaximumId);
        }
        finally
        {
            if (lease is not null)
            {
                using var releaseDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await importProvider.ReleaseLeaseAsync(lease, releaseDeadline.Token); }
                catch (Exception ex) { logger.LogWarning("IGDB import lease release failed; category {Category}.", ex.GetType().Name); }
            }
        }
    }

    private static IgdbImportRunResult CreateResult(int pages, int rows, int loaded, bool complete, bool leased,
        ImportWorkUnitBudget budget, bool includeProgress, ImportStopReason stop,
        int? admissionRemaining = null, bool catalogReady = false, bool IncrementalPending = false,
        DateTimeOffset? busyUntil = null, TimeSpan? retryAfter = null,
        int admissionRetryCount = 0, string? failingBatch = null, long? durableCursor = null, long? runMaximumId = null)
    {
        var result = new IgdbImportRunResult(pages, rows, loaded, complete, leased);
        if (!includeProgress)
            return result;
        return result with
        {
            StopReason = stop,
            AdmissionRemaining = admissionRemaining,
            CatalogReady = catalogReady,
            IncrementalPending = IncrementalPending,
            LeaseBusyUntil = busyUntil,
            HttpAttempts = budget.Session.HttpAttempts,
            AdmissionRows = budget.Session.AdmissionRows,
            AdmissionBatches = budget.Session.AdmissionBatches,
            DurableCursor = durableCursor,
            RunMaximumId = runMaximumId,
            AdmissionRetryCount = admissionRetryCount,
            FailingBatch = failingBatch,
            Elapsed = budget.Session.Elapsed,
            HttpAttemptsByOperation = budget.Session.HttpAttemptsByOperation,
            RetryAfter = retryAfter
        };
    }

    private static bool HasActiveRun(IgdbImportState state) => state.RunMaximumId is not null || state.RunUpdatedBefore is not null;

    private static ImportStopReason FetchStopReason(ImportWorkUnitBudget budget)
    {
        if (budget.Session.Remaining <= TimeSpan.Zero) return ImportStopReason.SessionDeadline;
        if (budget.Remaining <= TimeSpan.Zero) return ImportStopReason.UnitDeadline;
        if (budget.Session.HttpAttempts >= budget.Session.Limits.MaxHttpAttempts) return ImportStopReason.SessionHttpLimit;
        if (budget.HttpAttempts >= budget.Limits.MaxHttpAttempts) return ImportStopReason.UnitHttpLimit;
        if (budget.Session.AdmissionRows >= budget.Session.Limits.MaxAdmissionRows) return ImportStopReason.SessionAdmissionLimit;
        if (budget.RemainingAdmissionRows <= 0 || budget.RemainingAdmissionBatches <= 0) return ImportStopReason.UnitAdmissionLimit;
        return budget.Pages >= budget.Limits.MaxPages ? ImportStopReason.UnitPageLimit : ImportStopReason.None;
    }

    private static IgdbAdmissionResult MergeAdmission(IgdbAdmissionResult previous, IgdbAdmissionResult current) =>
        new(previous.LoadedRows + current.LoadedRows,
            previous.Batches + current.Batches,
            current.RemainingRows,
            current.Completed,
            current.Blocked,
            current.BlockReason ?? previous.BlockReason,
            current.BlockBatch ?? previous.BlockBatch,
            previous.RetryCount + current.RetryCount);

    private static bool IsValidCoverImageId(string? imageId) => imageId is { Length: > 0 and <= 255 }
        && imageId.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}

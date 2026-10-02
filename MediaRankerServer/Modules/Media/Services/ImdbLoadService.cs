using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Services;

public class ImdbLoadService
{
    private readonly IServiceScopeFactory? scopeFactory;
    private readonly IImdbLoadProvider? providerOverride;
    private readonly ImdbImportOptions config;
    private readonly ILogger<ImdbLoadService> logger;

    [ActivatorUtilitiesConstructor]
    public ImdbLoadService(
        IServiceScopeFactory scopeFactory,
        IOptions<ImdbImportOptions> options,
        ILogger<ImdbLoadService> logger)
    {
        this.scopeFactory = scopeFactory;
        config = options.Value;
        this.logger = logger;
    }

    // Compatibility constructor for isolated callers that supply a fake provider.
    public ImdbLoadService(
        IImdbLoadProvider loadProvider,
        IOptions<ImdbImportOptions> options,
        ILogger<ImdbLoadService> logger)
    {
        providerOverride = loadProvider;
        config = options.Value;
        this.logger = logger;
    }

    public Task<ImdbLoadResult> LoadAsync(CancellationToken ct = default) =>
        RunBoundedAsync(token => LoadAllAsync(null, token), ct);

    public Task<ImdbLoadResult> LoadAsync(ImdbImportExecution execution, CancellationToken ct = default) =>
        RunBoundedAsync(token => LoadAllAsync(execution, token), ct);

    private async Task<ImdbLoadResult> LoadAllAsync(ImdbImportExecution? execution, CancellationToken runCt)
    {
        var nonSeries = await LoadNonSeriesMediaCoreAsync(runCt, execution);
        var series = await LoadSeriesCollectionsCoreAsync(runCt, execution);
        var seasons = await LoadSeasonCollectionsCoreAsync(runCt, execution);
        var episodes = await LoadEpisodeMediaCoreAsync(runCt, execution);
        execution?.SetStage("load-complete");
        return new ImdbLoadResult(nonSeries.Affected + series.Affected + seasons.Affected + episodes.Affected);
    }

    public Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct = default) =>
        RunBoundedAsync(token => LoadNonSeriesMediaCoreAsync(token), ct);

    public Task<ImdbLoadResult> LoadSeriesCollectionsAsync(CancellationToken ct = default) =>
        RunBoundedAsync(token => LoadSeriesCollectionsCoreAsync(token), ct);

    public Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct = default) =>
        RunBoundedAsync(token => LoadSeasonCollectionsCoreAsync(token), ct);

    public Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct = default) =>
        RunBoundedAsync(token => LoadEpisodeMediaCoreAsync(token), ct);

    private Task<ImdbLoadResult> LoadNonSeriesMediaCoreAsync(CancellationToken ct, ImdbImportExecution? execution = null) =>
        DrainAsync("non-series", (provider, after, token) => provider.LoadNonSeriesMediaBatchAsync(config.MinVotesMovies, after, config.MaxLoadRowsPerUnit, token), ct, execution);

    private Task<ImdbLoadResult> LoadSeriesCollectionsCoreAsync(CancellationToken ct, ImdbImportExecution? execution = null) =>
        DrainAsync("series", (provider, after, token) => provider.LoadSeriesCollectionsBatchAsync(config.MinVotesTv, after, config.MaxLoadRowsPerUnit, token), ct, execution);

    private async Task<ImdbLoadResult> LoadSeasonCollectionsCoreAsync(CancellationToken ct, ImdbImportExecution? execution = null)
    {
        execution?.SetStage("load:seasons");
        logger.LogInformation("Starting IMDb load: season collections.");
        var total = 0;
        string? parent = null;
        int? season = null;
        while (true)
        {
            var batch = await WithProviderAsync((provider, token) => provider.LoadSeasonCollectionsBatchAsync(parent, season, config.MaxLoadRowsPerUnit, token), ct);
            total += batch.Affected;
            execution?.Counters.AddLoad(batch.Affected);
            if (!batch.HasMore)
            {
                logger.LogInformation("IMDb load stage {Stage} completed. Affected rows: {Affected}.", "seasons", total);
                return new ImdbLoadResult(total);
            }
            parent = batch.NextParentTconst;
            season = batch.NextSeasonNumber;
            await YieldAsync(ct);
        }
    }

    private async Task<ImdbLoadResult> LoadEpisodeMediaCoreAsync(CancellationToken ct, ImdbImportExecution? execution = null)
    {
        var result = await DrainAsync(
            "episodes",
            (provider, after, token) => provider.LoadEpisodeMediaBatchAsync(after, config.MaxLoadRowsPerUnit, token),
            ct,
            execution);
        await CleanupUnknownSeasonDataAsync(ct, execution);
        return result;
    }

    private async Task CleanupUnknownSeasonDataAsync(CancellationToken ct, ImdbImportExecution? execution)
    {
        execution?.SetStage("cleanup:unknown-season-episodes");
        logger.LogInformation("Starting IMDb cleanup stage {Stage}.", "unknown-season-episodes");
        var deletedEpisodes = 0;
        var skippedEpisodes = 0;
        long? afterMediaId = null;
        while (true)
        {
            var batch = await WithProviderAsync((provider, token) =>
                provider.DeleteUnknownSeasonEpisodesBatchAsync(afterMediaId, config.MaxCleanupRowsPerUnit, token), ct);
            deletedEpisodes += batch.Deleted;
            skippedEpisodes += batch.Skipped;
            execution?.Counters.AddCleanup(batch.Deleted);
            if (!batch.HasMore) break;
            afterMediaId = batch.NextMediaId;
            await YieldAsync(ct);
        }
        logger.LogInformation("IMDb cleanup stage {Stage} completed. Deleted {Deleted}; skipped {Skipped} reviewed episodes.",
            "unknown-season-episodes", deletedEpisodes, skippedEpisodes);

        execution?.SetStage("cleanup:unknown-seasons");
        logger.LogInformation("Starting IMDb cleanup stage {Stage}.", "unknown-seasons");
        var deletedSeasons = 0;
        var deletedSeries = 0;
        var skippedReviewedSeries = 0;
        long? afterSeasonId = null;
        while (true)
        {
            var batch = await WithProviderAsync((provider, token) =>
                provider.DeleteEmptyUnknownSeasonsBatchAsync(afterSeasonId, config.MaxCleanupRowsPerUnit, token), ct);
            deletedSeasons += batch.DeletedSeasons;
            deletedSeries += batch.DeletedSeries;
            skippedReviewedSeries += batch.SkippedReviewedSeries;
            execution?.Counters.AddCleanup(batch.DeletedSeasons + batch.DeletedSeries);
            if (!batch.HasMore) break;
            afterSeasonId = batch.NextSeasonId;
            await YieldAsync(ct);
        }
        logger.LogInformation("IMDb cleanup stage {Stage} completed. Deleted {Seasons} seasons and {Series} series; skipped {Skipped} reviewed series.",
            "unknown-seasons", deletedSeasons, deletedSeries, skippedReviewedSeries);
    }

    private async Task<T> RunBoundedAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        config.ValidateFiniteProfile();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(config.MaxWholeSessionSeconds));
        return await operation(deadline.Token);
    }

    private async Task<ImdbLoadResult> DrainAsync(
        string stage,
        Func<IImdbLoadProvider, string?, CancellationToken, Task<ImdbLoadBatchResult>> operation,
        CancellationToken ct,
        ImdbImportExecution? execution = null)
    {
        execution?.SetStage($"load:{stage}");
        logger.LogInformation("Starting IMDb load stage {Stage}.", stage);
        var total = 0;
        string? after = null;
        while (true)
        {
            var batch = await WithProviderAsync((provider, token) => operation(provider, after, token), ct);
            total += batch.Affected;
            execution?.Counters.AddLoad(batch.Affected);
            if (!batch.HasMore)
            {
                logger.LogInformation("IMDb load stage {Stage} completed. Affected rows: {Affected}.", stage, total);
                return new ImdbLoadResult(total);
            }
            after = batch.NextKey;
            await YieldAsync(ct);
        }
    }

    private async Task<T> WithProviderAsync<T>(Func<IImdbLoadProvider, CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (providerOverride is not null) return await operation(providerOverride, ct);
        using var scope = scopeFactory!.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<IImdbLoadProvider>(), ct);
    }

    private Task YieldAsync(CancellationToken ct) => config.YieldBetweenUnitsMilliseconds <= 0
        ? Task.CompletedTask
        : Task.Delay(config.YieldBetweenUnitsMilliseconds, ct);
}

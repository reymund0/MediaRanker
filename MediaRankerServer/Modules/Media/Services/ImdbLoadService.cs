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
        RunBoundedAsync(LoadAllAsync, ct);

    private async Task<ImdbLoadResult> LoadAllAsync(CancellationToken runCt)
    {
        var nonSeries = await LoadNonSeriesMediaCoreAsync(runCt);
        var series = await LoadSeriesCollectionsCoreAsync(runCt);
        var seasons = await LoadSeasonCollectionsCoreAsync(runCt);
        var episodes = await LoadEpisodeMediaCoreAsync(runCt);
        return new ImdbLoadResult(nonSeries.Affected + series.Affected + seasons.Affected + episodes.Affected);
    }

    public Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct = default) =>
        RunBoundedAsync(LoadNonSeriesMediaCoreAsync, ct);

    public Task<ImdbLoadResult> LoadSeriesCollectionsAsync(CancellationToken ct = default) =>
        RunBoundedAsync(LoadSeriesCollectionsCoreAsync, ct);

    public Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct = default) =>
        RunBoundedAsync(LoadSeasonCollectionsCoreAsync, ct);

    public Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct = default) =>
        RunBoundedAsync(LoadEpisodeMediaCoreAsync, ct);

    private Task<ImdbLoadResult> LoadNonSeriesMediaCoreAsync(CancellationToken ct) =>
        DrainAsync("non-series", (provider, after, token) => provider.LoadNonSeriesMediaBatchAsync(config.MinVotesMovies, after, config.MaxLoadRowsPerUnit, token), ct);

    private Task<ImdbLoadResult> LoadSeriesCollectionsCoreAsync(CancellationToken ct) =>
        DrainAsync("series", (provider, after, token) => provider.LoadSeriesCollectionsBatchAsync(config.MinVotesTv, after, config.MaxLoadRowsPerUnit, token), ct);

    private async Task<ImdbLoadResult> LoadSeasonCollectionsCoreAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting IMDb load: season collections.");
        var total = 0;
        string? parent = null;
        int? season = null;
        while (true)
        {
            var batch = await WithProviderAsync((provider, token) => provider.LoadSeasonCollectionsBatchAsync(parent, season, config.MaxLoadRowsPerUnit, token), ct);
            total += batch.Affected;
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

    private Task<ImdbLoadResult> LoadEpisodeMediaCoreAsync(CancellationToken ct) =>
        DrainAsync("episodes", (provider, after, token) => provider.LoadEpisodeMediaBatchAsync(after, config.MaxLoadRowsPerUnit, token), ct);

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
        CancellationToken ct)
    {
        logger.LogInformation("Starting IMDb load stage {Stage}.", stage);
        var total = 0;
        string? after = null;
        while (true)
        {
            var batch = await WithProviderAsync((provider, token) => operation(provider, after, token), ct);
            total += batch.Affected;
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

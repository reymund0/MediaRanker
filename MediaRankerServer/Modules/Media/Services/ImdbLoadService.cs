using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Jobs;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Services;

public class ImdbLoadService(
    IImdbLoadProvider loadProvider,
    IOptions<ImdbImportOptions> options,
    ILogger<ImdbLoadService> logger)
{
    private readonly ImdbImportOptions config = options.Value;

    public async Task<ImdbLoadResult> LoadAsync(CancellationToken ct = default)
    {
        var nonSeries = await LoadNonSeriesMediaAsync(ct);
        var series    = await LoadSeriesCollectionsAsync(ct);
        var seasons   = await LoadSeasonCollectionsAsync(ct);
        var episodes  = await LoadEpisodeMediaAsync(ct);
        return new ImdbLoadResult(nonSeries.Affected + series.Affected + seasons.Affected + episodes.Affected);
    }

    public async Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB load: non-series media.");

        var result = await loadProvider.LoadNonSeriesMediaAsync(config.MinVotesMovies, config.MinVotesVideoGames, ct);

        logger.LogInformation("IMDB load: non-series media completed. Affected rows: {Affected}", result.Affected);
        return result;
    }

    public async Task<ImdbLoadResult> LoadSeriesCollectionsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB load: series collections.");

        var result = await loadProvider.LoadSeriesCollectionsAsync(config.MinVotesTv, ct);

        logger.LogInformation("IMDB load: series collections completed. Affected rows: {Affected}", result.Affected);
        return result;
    }

    public async Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB load: season collections.");

        var result = await loadProvider.LoadSeasonCollectionsAsync(ct);

        logger.LogInformation("IMDB load: season collections completed. Affected rows: {Affected}", result.Affected);
        return result;
    }

    public async Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB load: episode media.");

        var result = await loadProvider.LoadEpisodeMediaAsync(ct);

        logger.LogInformation("IMDB load: episode media completed. Affected rows: {Affected}", result.Affected);
        return result;
    }
}

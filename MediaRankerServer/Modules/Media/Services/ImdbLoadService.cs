using MediaRankerServer.Modules.Media.Data;

namespace MediaRankerServer.Modules.Media.Services;

public class ImdbLoadService(IImdbLoadProvider loadProvider, ILogger<ImdbLoadService> logger)
{
    public async Task<ImdbLoadResult> LoadAsync(CancellationToken ct = default)
    {
        var nonSeries = await LoadNonSeriesMediaAsync(ct);
        var series    = await LoadSeriesCollectionsAsync(ct);
        var seasons   = await LoadSeasonCollectionsAsync(ct);
        return new ImdbLoadResult(nonSeries.Affected + series.Affected + seasons.Affected);
    }

    public async Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB load: non-series media.");

        var result = await loadProvider.LoadNonSeriesMediaAsync(ct);

        logger.LogInformation("IMDB load: non-series media completed. Affected rows: {Affected}", result.Affected);
        return result;
    }

    public async Task<ImdbLoadResult> LoadSeriesCollectionsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB load: series collections.");

        var result = await loadProvider.LoadSeriesCollectionsAsync(ct);

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
}

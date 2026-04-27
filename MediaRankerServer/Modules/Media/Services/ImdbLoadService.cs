using MediaRankerServer.Modules.Media.Data;

namespace MediaRankerServer.Modules.Media.Services;

public class ImdbLoadService(IImdbLoadProvider loadProvider, ILogger<ImdbLoadService> logger)
{
    public async Task<ImdbLoadResult> LoadAsync(CancellationToken ct = default)
    {
        var nonSeries = await LoadNonSeriesMediaAsync(ct);
        return nonSeries;
    }

    public async Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB load: non-series media.");

        var result = await loadProvider.LoadNonSeriesMediaAsync(ct);

        logger.LogInformation("IMDB load: non-series media completed. Affected rows: {Affected}", result.Affected);
        return result;
    }
}

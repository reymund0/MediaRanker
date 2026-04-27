using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public class ImdbImportOptions : BaseJobOptions
{
    public static readonly string SectionPath = "Media:ImdbImport";
    public override string JobName => "IMDB Import";
    public override bool Enabled { get; set; } = false;
    public override int ScheduleHourUtc { get; set; } = 3;
    public string DatasetUrl { get; set; } = "https://datasets.imdbws.com/title.basics.tsv.gz";
    public string EpisodesDatasetUrl { get; set; } = "https://datasets.imdbws.com/title.episode.tsv.gz";
    public int BatchSize { get; set; } = 5000;
}

public class ImdbImportJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ImdbImportOptions> options,
    ILogger<ImdbImportJob> logger) : BaseJob<ImdbImportOptions>(scopeFactory, options, logger)
{
    protected override async Task RunJobAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var importService = serviceProvider.GetRequiredService<ImdbImportService>();
        await importService.ImportAsync(ct);

        try
        {
            var loadService = serviceProvider.GetRequiredService<ImdbLoadService>();
            var loadResult = await loadService.LoadAsync(ct);
            logger.LogInformation("IMDB load completed. Affected rows: {Affected}", loadResult.Affected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IMDB load step failed; continuing.");
        }
    }
}

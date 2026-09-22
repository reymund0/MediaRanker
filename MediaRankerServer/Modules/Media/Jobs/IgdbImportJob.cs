using MediaRankerServer.Modules.Media.Providers;
using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public sealed class IgdbImportJob(
    IServiceScopeFactory scopeFactory,
    IOptions<IgdbOptions> options,
    ILogger<IgdbImportJob> logger) : BaseJob<IgdbOptions>(scopeFactory, options, logger)
{
    protected override async Task RunJobAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var result = await serviceProvider.GetRequiredService<IgdbImportService>().ImportAsync(ct);
        logger.LogInformation(
            "IGDB import run completed. Lease acquired: {LeaseAcquired}; Pages: {Pages}; Rows: {Rows}; Eligible games loaded: {Loaded}; Completed: {Completed}",
            result.LeaseAcquired, result.PagesCommitted, result.RowsCommitted, result.EligibleGamesLoaded, result.RunCompleted);
    }
}

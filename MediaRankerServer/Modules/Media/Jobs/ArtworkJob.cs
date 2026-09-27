using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Providers;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public class ArtworkJob(IServiceScopeFactory scopes, IOptions<ArtworkOptions> options,
    IOptions<IgdbOptions> igdb, IOptions<TmdbOptions> tmdb, ILogger<ArtworkJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Report configuration once without taking browsing/reviewing offline or logging credential values.
        if (igdb.Value.IsEnabled && !igdb.Value.IsUsable)
            logger.LogWarning("IGDB is enabled but its application credentials are missing; provider requests are disabled");
        if (tmdb.Value.Enabled && !tmdb.Value.IsUsable)
            logger.LogWarning("TMDB is enabled but its read token is missing; provider requests are disabled");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ArtworkProcessor>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Artwork processing failed; the next poll will retry"); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

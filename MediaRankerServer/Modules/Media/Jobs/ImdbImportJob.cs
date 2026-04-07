using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public class ImdbImportJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ImdbImportOptions> options,
    ILogger<ImdbImportJob> logger) : BackgroundService
{
    private readonly ImdbImportOptions config = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Enabled)
        {
            logger.LogInformation("IMDB import job is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate next run at scheduled hour UTC tomorrow
            var nowUtc = DateTimeOffset.UtcNow;
            var nextRunUtc = nowUtc.Date.AddHours(config.ScheduleHourUtc);
            if (nextRunUtc <= nowUtc)
            {
                nextRunUtc = nextRunUtc.AddDays(1);
            }

            var delay = nextRunUtc - nowUtc;

            logger.LogInformation("Next IMDB import job scheduled for {NextRun} UTC (Delay: {Delay})", nextRunUtc, delay);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<ImdbImportService>();
                await importService.ImportAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during IMDB import job.");
            }
        }
    }
}

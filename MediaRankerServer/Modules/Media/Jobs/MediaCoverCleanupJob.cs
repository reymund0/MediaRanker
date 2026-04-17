using MediaRankerServer.Modules.Media.Services;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public class MediaCoverCleanupJob(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaCoverCleanupOptions> options,
    ILogger<MediaCoverCleanupJob> logger) : BackgroundService
{
    private readonly string jobName = "Media Cover Cleanup";
    private readonly MediaCoverCleanupOptions config = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Enabled)
        {
            logger.LogInformation("{JobName} job is disabled.", jobName);
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

            logger.LogInformation(
                "Next {JobName} job scheduled for {NextRunUtc} UTC (Delay: {Delay})",
                jobName,
                nextRunUtc,
                delay);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<IMediaCoverCleanupService>();
                await cleanupService.CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during {JobName} job.", jobName);
            }
        }
    }
}

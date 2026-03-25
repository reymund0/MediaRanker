using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Files.Jobs;

public class FileUploadCleanupJob(
    IServiceScopeFactory scopeFactory,
    IOptions<FileCleanupOptions> options,
    ILogger<FileUploadCleanupJob> logger) : BackgroundService
{
    private readonly FileCleanupOptions config = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Enabled)
        {
            logger.LogInformation("File upload cleanup job is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRunUtc = GetNextNoonUtc();
            var delay = nextRunUtc - DateTimeOffset.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("Next file upload cleanup job scheduled for {NextRun} UTC (Delay: {Delay})", nextRunUtc, delay);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during file upload cleanup job.");
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting file upload cleanup job run.");

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSQLContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-config.StaleDaysThreshold);
        
        var staleUploads = await dbContext.FileUploads
            .Where(u => u.State == Entities.FileUploadState.Uploaded && u.UpdatedAt <= cutoff)
            .ToListAsync(stoppingToken);

        logger.LogInformation("Found {Count} stale uploaded files to clean up (Cutoff: {Cutoff})", staleUploads.Count, cutoff);

        int successCount = 0;
        int failCount = 0;

        foreach (var upload in staleUploads)
        {
            try
            {
                await mediator.Publish(new FileDeletedEvent(upload.FileKey, upload.EntityType.ToString()), stoppingToken);
                successCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish FileDeletedEvent for file {FileKey}", upload.FileKey);
                failCount++;
            }
        }

        logger.LogInformation("File upload cleanup job completed. Success: {Success}, Failed: {Failed}", successCount, failCount);
    }

    private DateTimeOffset GetNextNoonUtc()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var todayNoonUtc = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, 12, 0, 0, TimeSpan.Zero);

        if (nowUtc >= todayNoonUtc)
        {
            return todayNoonUtc.AddDays(1);
        }

        return todayNoonUtc;
    }
}

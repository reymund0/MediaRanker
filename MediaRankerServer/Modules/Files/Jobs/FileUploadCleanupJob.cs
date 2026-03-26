using MediaRankerServer.Modules.Files.Entities;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Files.Jobs;

// TODO: Convert this to something industry-standard like Quartz.NET or Hangfire.
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
            // Calculate the next run for Noon UTC tomorrow
            var tomorrowNoonUtc = DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(12);
            var nextRunUtc = new DateTimeOffset(tomorrowNoonUtc, TimeSpan.Zero);

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
        var cleanupRunner = scope.ServiceProvider.GetRequiredService<FileUploadCleanupRunner>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-config.StaleDaysThreshold);

        await cleanupRunner.RunAsync(cutoff, stoppingToken);
    }
}

public class FileUploadCleanupRunner(
    PostgreSQLContext dbContext,
    IMediator mediator,
    ILogger<FileUploadCleanupRunner> logger)
{
    public async Task RunAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        var staleUploads = await dbContext.FileUploads
            .Where(u => u.State == FileUploadState.Uploaded && u.UpdatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} stale uploaded files to clean up (Cutoff: {Cutoff})", staleUploads.Count, cutoff);

        int successCount = 0;
        int failCount = 0;

        foreach (var upload in staleUploads)
        {
            try
            {
                await mediator.Publish(new FileDeletedEvent(upload.FileKey, upload.EntityType.ToString()), cancellationToken);
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
}

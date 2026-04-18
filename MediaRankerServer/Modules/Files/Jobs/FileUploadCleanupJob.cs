using MediaRankerServer.Modules.Files.Data.Entities;
using MediaRankerServer.Modules.Files.Events;
using MediaRankerServer.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Files.Jobs;

public class FileCleanupOptions : BaseJobOptions
{
    public static readonly string SectionPath = "Files:Cleanup";
    public override string JobName => "File Upload Cleanup";
    public override bool Enabled { get; set; } = true;
    public override int ScheduleHourUtc { get; set; } = 12;
    public int StaleDaysThreshold { get; set; } = 2;
}

public class FileUploadCleanupJob(
    IServiceScopeFactory scopeFactory,
    IOptions<FileCleanupOptions> options,
    ILogger<FileUploadCleanupJob> logger) : BaseJob<FileCleanupOptions>(scopeFactory, options, logger)
{    
    protected override async Task RunJobAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting file upload cleanup job run.");

        var dbContext = serviceProvider.GetRequiredService<PostgreSQLContext>();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-config.StaleDaysThreshold);

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
            catch (TaskCanceledException)
            {
                // Bubble up the cancellation.
                throw;
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

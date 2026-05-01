using MediaRankerServer.Modules.Media.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

public class MediaCoverCleanupOptions : BaseJobOptions
{
    public static readonly string SectionPath = "Media:MediaCoverCleanup";
    public override string JobName => "Media Cover Cleanup";
    public override bool Enabled { get; set; } = false;
    public override int ScheduleHourUtc { get; set; } = 3;
}


public class MediaCoverCleanupJob(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaCoverCleanupOptions> options,
    ILogger<MediaCoverCleanupJob> logger) : BaseJob<MediaCoverCleanupOptions>(scopeFactory, options, logger)
{
    protected override async Task RunJobAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var cleanupService = serviceProvider.GetRequiredService<IMediaCoverCleanupService>();
        await cleanupService.CleanupAsync(ct);
    }
}

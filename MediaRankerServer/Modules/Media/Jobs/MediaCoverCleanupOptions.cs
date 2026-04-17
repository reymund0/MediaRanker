namespace MediaRankerServer.Modules.Media.Jobs;

public class MediaCoverCleanupOptions
{
    public const string SectionName = "Media:MediaCoverCleanup";

    public bool Enabled { get; set; } = false;
    public int ScheduleHourUtc { get; set; } = 3; // 3 AM UTC
}

namespace MediaRankerServer.Modules.Files.Jobs;

public class FileCleanupOptions
{
    public const string SectionName = "Files:Cleanup";

    public bool Enabled { get; set; } = true;
    public int StaleDaysThreshold { get; set; } = 1;
}

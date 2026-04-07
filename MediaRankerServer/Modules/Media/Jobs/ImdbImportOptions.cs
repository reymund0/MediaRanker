namespace MediaRankerServer.Modules.Media.Jobs;

public class ImdbImportOptions
{
    public const string SectionName = "Media:ImdbImport";

    public bool Enabled { get; set; } = false;
    public string DatasetUrl { get; set; } = "https://datasets.imdbws.com/title.basics.tsv.gz";
    public int BatchSize { get; set; } = 5000;
    public int ScheduleHourUtc { get; set; } = 3; // 3 AM UTC
}

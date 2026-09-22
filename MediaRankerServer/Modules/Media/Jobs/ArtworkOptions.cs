namespace MediaRankerServer.Modules.Media.Jobs;

public class ArtworkOptions
{
    public const string SectionPath = "Media:Artwork";
    public int PositiveCacheDays { get; set; } = 30;
    public int NegativeCacheDays { get; set; } = 7;
    public int PollSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 120;
    public int MaxAttempts { get; set; } = 5;
    public int RetrySeconds { get; set; } = 60;
    public int MaxRetrySeconds { get; set; } = 21600;

    public bool IsValid() => PositiveCacheDays is > 0 and <= 150 && NegativeCacheDays is > 0 and <= 150
        && PollSeconds is > 0 and <= 60 && BatchSize is > 0 and <= 100
        && LeaseSeconds is >= 60 and <= 600 && MaxAttempts is > 0 and <= 10
        && RetrySeconds > 0 && MaxRetrySeconds >= RetrySeconds && MaxRetrySeconds <= 86400;
}

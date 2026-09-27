namespace MediaRankerServer.Modules.Media.Providers;

public sealed class TmdbOptions
{
    public const string SectionPath = "Media:Tmdb";

    public bool Enabled { get; set; }
    public string? ReadAccessToken { get; set; }
    public int TimeoutSeconds { get; set; } = 20;
    public int RequestsPerSecond { get; set; } = 4;
    public int MaxConcurrentRequests { get; set; } = 4;

    public bool IsUsable => Enabled && !string.IsNullOrWhiteSpace(ReadAccessToken) && TimeoutSeconds is >= 1 and <= 120;
    public bool HasValidLimits => TimeoutSeconds is >= 1 and <= 120
        && RequestsPerSecond is >= 1 and <= 20 && MaxConcurrentRequests is >= 1 and <= 8;

    public bool IsValid(out string? failure)
    {
        if (!HasValidLimits)
        {
            failure = "TMDB timeout must be between one and 120 seconds.";
            return false;
        }

        if (Enabled && string.IsNullOrWhiteSpace(ReadAccessToken))
        {
            failure = "TMDB is enabled but its read access token is not configured.";
            return false;
        }

        failure = null;
        return true;
    }
}

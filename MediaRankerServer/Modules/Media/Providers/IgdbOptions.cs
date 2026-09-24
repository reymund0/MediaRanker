namespace MediaRankerServer.Modules.Media.Providers;

public sealed class IgdbOptions : BaseJobOptions
{
    public const string SectionPath = "Media:Igdb";

    public override string JobName => "IGDB Import";
    public override bool Enabled { get => ImportEnabled; set => ImportEnabled = value; }
    public override int ScheduleHourUtc { get; set; } = 3;
    public bool ImportEnabled { get; set; }
    public bool ArtworkEnabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public int RequestsPerSecond { get; set; } = 3;
    public int MaxConcurrentRequests { get; set; } = 8;
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxStatementSeconds { get; set; } = 15;
    public int PageSize { get; set; } = 100;
    // PageBudget is retained for the existing scheduled profile. Bootstrap jobs
    // use the explicit finite work-unit limits below.
    public int PageBudget { get; set; } = 10;
    public int WorkUnitPageLimit { get; set; } = 5;
    public int WorkUnitHttpAttemptLimit { get; set; } = 20;
    public int WorkUnitAdmissionRowLimit { get; set; } = 500;
    public int WorkUnitAdmissionBatchLimit { get; set; } = 1;
    public int WorkUnitSeconds { get; set; } = 60;
    public int ScheduledSessionHttpAttemptLimit { get; set; } = 100;
    public int ScheduledSessionAdmissionRowLimit { get; set; } = 5000;
    public int ScheduledSessionMinutes { get; set; } = 15;
    public int IncrementalOverlapMinutes { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 120;

    public bool IsEnabled => ImportEnabled || ArtworkEnabled;
    public bool IsUsable => IsEnabled && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public bool HasValidLimits => ScheduleHourUtc is >= 0 and <= 23 && RequestsPerSecond is >= 1 and <= 4
        && MaxConcurrentRequests is >= 1 and <= 8 && TimeoutSeconds is >= 1 and <= 120
        && MaxStatementSeconds is >= 1 and <= 120
        && PageSize is >= 1 and <= 500 && PageBudget is >= 1 and <= 1000
        && WorkUnitPageLimit is >= 1 and <= 1000
        && WorkUnitHttpAttemptLimit is >= 1 and <= 10000
        && WorkUnitAdmissionRowLimit is >= 1 and <= 100000
        && WorkUnitAdmissionBatchLimit is >= 1 and <= 1000
        && WorkUnitSeconds is >= 1 and <= 3600
        && ScheduledSessionHttpAttemptLimit is >= 1 and <= 100000
        && ScheduledSessionAdmissionRowLimit is >= 1 and <= 1000000
        && ScheduledSessionMinutes is >= 1 and <= 1440
        && IncrementalOverlapMinutes is >= 1 and <= 1440 && LeaseSeconds is >= 60 and <= 600;

    public bool IsValid(out string? failure)
    {
        if (!HasValidLimits)
        {
            failure = "IGDB options contain an out-of-range request budget or import setting.";
            return false;
        }

        if (IsEnabled && (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret)))
        {
            failure = "IGDB is enabled but Twitch application credentials are not configured.";
            return false;
        }

        failure = null;
        return true;
    }
}

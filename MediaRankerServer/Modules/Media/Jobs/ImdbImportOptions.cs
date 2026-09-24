using Microsoft.Extensions.Options;

namespace MediaRankerServer.Modules.Media.Jobs;

/// <summary>
/// Configuration for the IMDb dataset replay.  Every production invocation has
/// finite limits; the calibration flag is an explicit operator review gate and
/// is intentionally false by default.
/// </summary>
public sealed class ImdbImportOptions : BaseJobOptions
{
    public static readonly string SectionPath = "Media:ImdbImport";
    public const int MaximumBatchCharacters = 32 * 1024 * 1024;
    public const int DefaultBatchSize = 5_000;
    // Keep reference/object overhead bounded even when accepted TSV rows are very short.
    public const int MaximumBatchSize = 50_000;

    public override string JobName => "IMDB Import";
    public override bool Enabled { get; set; }
    public override int ScheduleHourUtc { get; set; } = 3;

    public string DatasetUrl { get; set; } = "https://datasets.imdbws.com/title.basics.tsv.gz";
    public string EpisodesDatasetUrl { get; set; } = "https://datasets.imdbws.com/title.episode.tsv.gz";
    public string RatingsDatasetUrl { get; set; } = "https://datasets.imdbws.com/title.ratings.tsv.gz";
    public int BatchSize { get; set; } = DefaultBatchSize;
    public int MinVotesMovies { get; set; } = 1_000;
    public int MinVotesTv { get; set; } = 1_000;

    public bool CalibratedProfileConfirmed { get; set; }
    public int MaxHttpAttempts { get; set; } = 3;
    public long MaxCompressedBytesPerFeed { get; set; } = 64L * 1024 * 1024;
    public long MaxTemporaryDiskBytes { get; set; } = 128L * 1024 * 1024;
    public long MaxDecompressedBytesPerFeed { get; set; } = 512L * 1024 * 1024;
    public int MaxWholeSessionSeconds { get; set; } = 900;
    public long MaxRowsPerFeed { get; set; } = 5_000_000;
    public int MaxLineCharacters { get; set; } = 1_048_576;
    /// <summary>Maximum combined raw TSV characters retained in one parsed batch.</summary>
    public int MaxBatchCharacters { get; set; } = 8 * 1024 * 1024;
    public int MaxLoadRowsPerUnit { get; set; } = 1_000;
    public int MaxCleanupRowsPerUnit { get; set; } = 1_000;
    public int MaxStatementSeconds { get; set; } = 15;
    public int YieldBetweenUnitsMilliseconds { get; set; }

    public bool HasFiniteProfile(out string reason)
    {
        if (ScheduleHourUtc is < 0 or > 23) return Fail("ScheduleHourUtc must be between 0 and 23.", out reason);
        if (MaxHttpAttempts < 3) return Fail("MaxHttpAttempts must be at least three for the required feeds.", out reason);
        if (MaxCompressedBytesPerFeed <= 0) return Fail("MaxCompressedBytesPerFeed must be positive.", out reason);
        if (MaxTemporaryDiskBytes <= 0) return Fail("MaxTemporaryDiskBytes must be positive.", out reason);
        if (MaxDecompressedBytesPerFeed <= 0) return Fail("MaxDecompressedBytesPerFeed must be positive.", out reason);
        if (MaxWholeSessionSeconds is <= 0 or > 604_800)
            return Fail("MaxWholeSessionSeconds must be between one second and seven days.", out reason);
        if (MaxRowsPerFeed <= 0) return Fail("MaxRowsPerFeed must be positive.", out reason);
        if (MaxLineCharacters <= 0) return Fail("MaxLineCharacters must be positive.", out reason);
        if (MaxBatchCharacters is <= 0 or > MaximumBatchCharacters)
            return Fail($"MaxBatchCharacters must be between one and {MaximumBatchCharacters} characters.", out reason);
        if (MaxLineCharacters > MaxBatchCharacters)
            return Fail("MaxLineCharacters cannot exceed MaxBatchCharacters.", out reason);
        if (MaxLoadRowsPerUnit <= 0) return Fail("MaxLoadRowsPerUnit must be positive.", out reason);
        if (MaxCleanupRowsPerUnit <= 0) return Fail("MaxCleanupRowsPerUnit must be positive.", out reason);
        if (MaxStatementSeconds <= 0) return Fail("MaxStatementSeconds must be positive.", out reason);
        if (YieldBetweenUnitsMilliseconds < 0) return Fail("YieldBetweenUnitsMilliseconds cannot be negative.", out reason);
        if (BatchSize is <= 0 or > MaximumBatchSize)
            return Fail($"BatchSize must be between one and {MaximumBatchSize} rows.", out reason);
        if (string.IsNullOrWhiteSpace(DatasetUrl) || string.IsNullOrWhiteSpace(EpisodesDatasetUrl) || string.IsNullOrWhiteSpace(RatingsDatasetUrl))
            return Fail("All IMDb dataset URLs are required.", out reason);

        reason = string.Empty;
        return true;
    }

    public void ValidateFiniteProfile()
    {
        if (!HasFiniteProfile(out var reason))
            throw new OptionsValidationException(nameof(ImdbImportOptions), typeof(ImdbImportOptions), [reason]);
    }

    private static bool Fail(string message, out string reason)
    {
        reason = message;
        return false;
    }
}

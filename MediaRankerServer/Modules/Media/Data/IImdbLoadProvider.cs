namespace MediaRankerServer.Modules.Media.Data;

public record ImdbLoadResult(int Affected);
public record ImdbLoadBatchResult(int Affected, string? NextKey, bool HasMore);
public record ImdbSeasonLoadBatchResult(int Affected, string? NextParentTconst, int? NextSeasonNumber, bool HasMore);
public record ImdbUnknownEpisodeCleanupBatchResult(int Deleted, int Skipped, long? NextMediaId, bool HasMore);
public record ImdbUnknownSeasonCleanupBatchResult(int DeletedSeasons, int DeletedSeries, int SkippedReviewedSeries, long? NextSeasonId, bool HasMore);

public interface IImdbLoadProvider
{
    Task<ImdbLoadResult> LoadNonSeriesMediaAsync(int minVotesMovies, CancellationToken ct);
    Task<ImdbLoadResult> LoadSeriesCollectionsAsync(int minVotesTv, CancellationToken ct);
    Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct);
    Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct);

    Task<ImdbLoadBatchResult> LoadNonSeriesMediaBatchAsync(int minVotesMovies, string? afterTconst, int maxRows, CancellationToken ct);
    Task<ImdbLoadBatchResult> LoadSeriesCollectionsBatchAsync(int minVotesTv, string? afterTconst, int maxRows, CancellationToken ct);
    Task<ImdbSeasonLoadBatchResult> LoadSeasonCollectionsBatchAsync(string? afterParentTconst, int? afterSeasonNumber, int maxGroups, CancellationToken ct);
    Task<ImdbLoadBatchResult> LoadEpisodeMediaBatchAsync(string? afterTconst, int maxRows, CancellationToken ct);
    Task<ImdbUnknownEpisodeCleanupBatchResult> DeleteUnknownSeasonEpisodesBatchAsync(long? afterMediaId, int maxRows, CancellationToken ct);
    Task<ImdbUnknownSeasonCleanupBatchResult> DeleteEmptyUnknownSeasonsBatchAsync(long? afterSeasonId, int maxRows, CancellationToken ct);
}

namespace MediaRankerServer.Modules.Media.Data;

public record ImdbImportResult(int Affected, int Skipped);
public record ImdbCleanupBatchResult(int Affected, long? NextId, bool HasMore);

public interface IImdbImportProvider
{
    Task<ImdbImportResult> ImportEpisodesAsync(List<ImdbEpisodeTsvRow> rows, CancellationToken ct);
    Task<ImdbImportResult> ImportBasicsAsync(List<ImdbTsvRow> rows, CancellationToken ct);
    Task<ImdbImportResult> ImportRatingsAsync(List<ImdbRatingTsvRow> rows, CancellationToken ct);

    Task<DateTimeOffset> GetDatabaseUtcNowAsync(CancellationToken ct);
    Task<int> DeleteFutureImportsAsync(int maxRows, CancellationToken ct);
    Task<int> DeleteTvPilotImportsAsync(int maxRows, CancellationToken ct);
    Task<int> DeleteOrphanEpisodesAsync(int maxRows, CancellationToken ct);
    Task<ImdbCleanupBatchResult> DeleteOrphanEpisodesBatchAsync(long? afterId, int maxRows, CancellationToken ct);
    Task<int> DeleteStaleRatingsAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct);

    // Compatibility overloads for existing callers. New ingestion uses bounded overloads.
    Task<int> DeleteFutureImportsAsync(CancellationToken ct);
    Task<int> DeleteTvPilotImportsAsync(CancellationToken ct);
    Task<int> DeleteOrphanEpisodesAsync(CancellationToken ct);
    Task<int> DeleteStaleRatingsAsync(DateTimeOffset cutoffUtc, CancellationToken ct);
}

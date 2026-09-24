using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.Modules.Media.Data;

public sealed class IgdbImportLease(IgdbImportState state, Guid token, TimeSpan leaseDuration)
{
    public IgdbImportState State { get; set; } = state;
    public Guid Token { get; } = token;
    public TimeSpan LeaseDuration { get; } = leaseDuration;
}

public sealed record IgdbAdmissionResult(
    int LoadedRows,
    int Batches,
    int? RemainingRows,
    bool Completed,
    bool Blocked,
    string? BlockReason = null,
    string? BlockBatch = null,
    int RetryCount = 0);

public interface IIgdbImportProvider
{
    Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct);
    Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? maximumId, DateTimeOffset? updatedAfter, DateTimeOffset? updatedBefore, DateTimeOffset now, CancellationToken ct);
    Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long committedId, CancellationToken ct);
    Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct);
    Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct);
    Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> supportedGameTypes, DateTimeOffset now, TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct);

    async Task<IgdbAdmissionResult> LoadEligibleGamesAsync(
        IgdbImportLease lease,
        IReadOnlySet<string> supportedGameTypes,
        DateTimeOffset now,
        TimeSpan positiveCacheDuration,
        TimeSpan negativeCacheDuration,
        ImportWorkUnitBudget budget,
        CancellationToken ct)
    {
        var loaded = await LoadEligibleGamesAsync(lease, supportedGameTypes, now, positiveCacheDuration, negativeCacheDuration, ct);
        return new IgdbAdmissionResult(loaded, loaded == 0 ? 0 : 1, null, true, false);
    }

    Task<DateTimeOffset?> GetLeaseBusyUntilAsync(CancellationToken ct) => Task.FromResult<DateTimeOffset?>(null);
}

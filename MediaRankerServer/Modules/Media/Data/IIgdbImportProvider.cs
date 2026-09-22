using MediaRankerServer.Modules.Media.Data.Entities;

namespace MediaRankerServer.Modules.Media.Data;

public sealed class IgdbImportLease(IgdbImportState state, Guid token, TimeSpan leaseDuration)
{
    public IgdbImportState State { get; set; } = state;
    public Guid Token { get; } = token;
    public TimeSpan LeaseDuration { get; } = leaseDuration;
}

public interface IIgdbImportProvider
{
    Task<IgdbImportLease?> TryAcquireLeaseAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct);
    Task<IgdbImportState> StartRunAsync(IgdbImportLease lease, bool bootstrap, long? maximumId, DateTimeOffset? updatedAfter, DateTimeOffset? updatedBefore, DateTimeOffset now, CancellationToken ct);
    Task<IgdbImportState> CommitPageAsync(IgdbImportLease lease, IReadOnlyList<IgdbImport> page, long committedId, CancellationToken ct);
    Task CompleteRunAsync(IgdbImportLease lease, DateTimeOffset now, CancellationToken ct);
    Task ReleaseLeaseAsync(IgdbImportLease lease, CancellationToken ct);
    Task<int> LoadEligibleGamesAsync(IgdbImportLease lease, IReadOnlySet<string> supportedGameTypes, DateTimeOffset now, TimeSpan positiveCacheDuration, TimeSpan negativeCacheDuration, CancellationToken ct);
}

namespace MediaRankerServer.Modules.Media.Providers;

public interface IIgdbClient
{
    Task<ArtworkResult> GetCoverAsync(string gameId, CancellationToken ct);
    Task<long> GetMaximumGameIdAsync(CancellationToken ct);
    Task<IReadOnlyList<IgdbGameType>> GetGameTypesAsync(CancellationToken ct);
    Task<IReadOnlyList<IgdbGame>> GetGamesAsync(IgdbGameQuery query, CancellationToken ct);
}

public sealed record IgdbGameType(long Id, string Name);

public sealed record IgdbGame(
    long Id,
    string? Name,
    DateTimeOffset? FirstReleaseDate,
    long? GameTypeId,
    long? VersionParentId,
    string? CoverImageId,
    DateTimeOffset? UpdatedAt);

public sealed record IgdbGameQuery(
    long AfterId,
    long? MaximumId,
    DateTimeOffset? UpdatedAfter,
    DateTimeOffset? UpdatedBefore,
    int Limit);

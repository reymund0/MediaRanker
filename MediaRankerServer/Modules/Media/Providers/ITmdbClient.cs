namespace MediaRankerServer.Modules.Media.Providers;

public interface ITmdbClient
{
    Task<ArtworkResult> GetCoverAsync(string imdbId, bool series, CancellationToken ct);
}

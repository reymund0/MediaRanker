namespace MediaRankerServer.Modules.Media.Data;

public record ImdbLoadResult(int Affected);

public interface IImdbLoadProvider
{
    Task<ImdbLoadResult> LoadNonSeriesMediaAsync(int minVotes, CancellationToken ct);
    Task<ImdbLoadResult> LoadSeriesCollectionsAsync(int minVotes, CancellationToken ct);
    Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct);
    Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct);
}

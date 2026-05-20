namespace MediaRankerServer.Modules.Media.Data;

public record ImdbLoadResult(int Affected);
public interface IImdbLoadProvider
{
    Task<ImdbLoadResult> LoadNonSeriesMediaAsync(int minVotesMovies, int minVotesVideoGames, CancellationToken ct);
    Task<ImdbLoadResult> LoadSeriesCollectionsAsync(int minVotesTv, CancellationToken ct);
    Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct);
    Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct);
}

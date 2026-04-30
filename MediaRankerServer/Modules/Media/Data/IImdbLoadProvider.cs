namespace MediaRankerServer.Modules.Media.Data;

public record ImdbLoadResult(int Affected);

public interface IImdbLoadProvider
{
    Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct);
    Task<ImdbLoadResult> LoadSeriesCollectionsAsync(CancellationToken ct);
    Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct);
}

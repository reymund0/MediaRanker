namespace MediaRankerServer.Modules.Media.Data;

public record ImdbLoadResult(int Affected);

public interface IImdbLoadProvider
{
    Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct);
}

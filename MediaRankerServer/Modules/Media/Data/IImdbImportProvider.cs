namespace MediaRankerServer.Modules.Media.Data;

public record ImdbImportResult(int Inserted, int Skipped);

public interface IImdbImportProvider
{
  Task<ImdbImportResult> ImportEpisodesAsync(List<ImdbEpisodeTsvRow> rows, CancellationToken ct);
  Task<int> DeleteFutureImportsAsync(CancellationToken ct);
  Task<int> DeleteTvPilotImportsAsync(CancellationToken ct);
  Task<ImdbImportResult> ImportBasicsAsync(List<ImdbTsvRow> rows, CancellationToken ct);
  Task<int> DeleteOrphanEpisodesAsync(CancellationToken ct);
}
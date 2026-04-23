using MediaRankerServer.Shared.Data;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Modules.Media.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;

namespace MediaRankerServer.Modules.Media.Services;

public record ImdbImportRunResult(ImdbImportResult Basics, ImdbImportResult? Episodes);

public class ImdbImportService(
    ImdbTsvProvider parser,
    IImdbImportProvider importProvider,
    IOptions<ImdbImportOptions> options,
    ILogger<ImdbImportService> logger)
{
    private readonly ImdbImportOptions config = options.Value;
    private int basicsInserted = 0;
    private int basicsSkipped = 0;
    private int episodesInserted = 0;
    private int episodesSkipped = 0;

    // Headers for IMDB datasets
    private static readonly string[] BasicsHeaders = [
        "tconst", "titleType", "primaryTitle", "originalTitle",
        "isAdult", "startYear", "endYear", "runtimeMinutes", "genres"
    ];

    private static readonly string[] EpisodeHeaders = [
        "tconst", "parentTconst", "seasonNumber", "episodeNumber"
    ];

    public async Task<ImdbImportRunResult> ImportAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB import job run.");

        var basics = await ImportBasicsAsync(ct);
        var episodes = await ImportEpisodesAsync(ct);
        return new ImdbImportRunResult(basics, episodes);
    }

    private async Task<ImdbImportResult> ImportBasicsAsync(CancellationToken ct)
    {
        await parser.RunBatchImportAsync<ImdbTsvRow>(
            config.DatasetUrl,
            BasicsHeaders,
            ParseBasicsRow,
            ImportBasicsBatchAsync,
            ct);

        logger.LogInformation("IMDB basics import completed. Inserted: {Inserted}, Skipped: {Skipped}",
            basicsInserted, basicsSkipped);
        
        // Delete unwanted rows from imdb_imports.
        try 
        {
            var deletedTvPilot = await importProvider.DeleteTvPilotImportsAsync(ct);
            logger.LogInformation("Deleted {Count} TV pilot imports", deletedTvPilot);
            
            var deletedFuture = await importProvider.DeleteFutureImportsAsync(ct);
            logger.LogInformation("Deleted {Count} future imports", deletedFuture);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting unwanted rows from imdb_imports, continuing with import");
        }

        return new ImdbImportResult(basicsInserted, basicsSkipped);
    }

    private async Task ImportBasicsBatchAsync(List<ImdbTsvRow> batch, CancellationToken ct)
    {
        try
        {
            var result = await importProvider.ImportBasicsAsync(batch, ct);
            basicsInserted += result.Inserted;
            basicsSkipped += result.Skipped;
        }
        catch (Exception ex)
        {
            // Don't let a single batch failure stop the entire import
            logger.LogError(ex, "Error importing basics batch");
        }
    }

    private static ImdbTsvRow? ParseBasicsRow(string[] columns, int lineNumber, string line)
    {
        // Skip adult content
        if (columns[4] == "1")
        {
            return null;
        }

        return new ImdbTsvRow(
            Tconst: columns[0],
            TitleType: columns[1],
            PrimaryTitle: SanitizeTitle(columns[2]),
            OriginalTitle: SanitizeTitle(columns[3]),
            IsAdult: columns[4] == "1",
            StartYear: ParseNullableInt(columns[5]),
            EndYear: ParseNullableInt(columns[6]),
            RuntimeMinutes: ParseNullableInt(columns[7]),
            Genres: columns[8] == @"\N" ? null : columns[8],
            RawLine: line
        );
    }

    private async Task<ImdbImportResult> ImportEpisodesAsync(CancellationToken ct)
    {
        await parser.RunBatchImportAsync<ImdbEpisodeTsvRow>(
            config.EpisodesDatasetUrl,
            EpisodeHeaders,
            ParseEpisodeRow,
            ImportEpisodesBatchAsync,
            ct);

        logger.LogInformation("IMDB episodes import completed. Inserted: {Inserted}, Skipped: {Skipped}",
            episodesInserted, episodesSkipped);

        // Because the imported episodes don't contain isAdult flag, we need to clean those up ourselves.
        try
        {
            var deletedCount = await importProvider.DeleteOrphanEpisodesAsync(ct);
            logger.LogInformation(
                "Cleaned up {Count} orphan rows from imdb_import_episodes with no matching imdb_imports entry",
                deletedCount >= 0 ? deletedCount.ToString() : "unknown");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete orphan rows from imdb_import_episodes; continuing.");
        }

        return new ImdbImportResult(episodesInserted, episodesSkipped);
    }

    private async Task ImportEpisodesBatchAsync(List<ImdbEpisodeTsvRow> batch, CancellationToken ct)
    {
        try
        {
            var result = await importProvider.ImportEpisodesAsync(batch, ct);
            episodesInserted += result.Inserted;
            episodesSkipped += result.Skipped;
        }
        catch (Exception ex)
        {
            // Don't let a single batch failure stop the entire import
            logger.LogError(ex, "Error importing episodes batch");
        }
    }

    private static ImdbEpisodeTsvRow ParseEpisodeRow(string[] columns, int lineNumber, string line)
    {
        var seasonNumber = ParseNullableInt(columns[2]);
        var episodeNumber = ParseNullableInt(columns[3]);

        return new ImdbEpisodeTsvRow(
            Tconst: columns[0],
            ParentTconst: columns[1],
            // NULL Season/Episode should never actually happen in the imdb dataset, but enter as -1 so we can find them later.
            SeasonNumber: seasonNumber ?? -1,
            EpisodeNumber: episodeNumber ?? -1,
            RawLine: line
        );
    }

    private static string SanitizeTitle(string title)
    {
        return title.Replace("{", "").Replace("}", "");
    }

    private static int? ParseNullableInt(string value)
    {
        if (value == @"\N" || string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (int.TryParse(value, out var result))
        {
            return result;
        }

        return null;
    }
}


using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MediaRankerServer.Modules.Media.Data;

public class ImdbImportSqlProvider(PostgreSQLContext dbContext, ILogger<ImdbImportSqlProvider> logger) : IImdbImportProvider
{   
  public async Task<ImdbImportResult> ImportBasicsAsync(List<ImdbTsvRow> rows, CancellationToken ct)
    {
        string sql = string.Empty;
        try
        {
            sql = BuildBasicsInsertSql(rows);
            var result = await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            var skipped = rows.Count - result;
            return new ImdbImportResult(result, skipped);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing batch of IMDB basics rows. SQL: {Sql}", sql);
            throw;
        }
    }

    public async Task<ImdbImportResult> ImportEpisodesAsync(List<ImdbEpisodeTsvRow> rows, CancellationToken ct)
    {
        string sql = string.Empty;
        try
        {
            sql = BuildEpisodesInsertSql(rows);
            var result = await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            var skipped = rows.Count - result;
            return new ImdbImportResult(result, skipped);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing batch of IMDB episodes rows. SQL: {Sql}", sql);
            throw;
        }
    }

    private static string BuildBasicsInsertSql(List<ImdbTsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INSERT INTO imdb_imports (tconst, title_type, primary_title, original_title, is_adult, start_year, end_year, runtime_minutes, genres)");
        sb.AppendLine("VALUES");

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var comma = i < rows.Count - 1 ? "," : "";

            var startYear = row.StartYear.HasValue ? row.StartYear.Value.ToString() : "NULL";
            var endYear = row.EndYear.HasValue ? row.EndYear.Value.ToString() : "NULL";
            var runtime = row.RuntimeMinutes.HasValue ? row.RuntimeMinutes.Value.ToString() : "NULL";
            var genres = row.Genres != null ? "'" + EscapeSql(row.Genres) + "'" : "NULL";

            sb.AppendLine($"    ('" + EscapeSql(row.Tconst) + "', '" + EscapeSql(row.TitleType) + "', '" + EscapeSql(row.PrimaryTitle) + "', '" + EscapeSql(row.OriginalTitle) + "', " + BoolToSql(row.IsAdult) + ", " + startYear + ", " + endYear + ", " + runtime + ", " + genres + ")" + comma);
        }

        sb.AppendLine("ON CONFLICT (tconst) DO NOTHING");

        return sb.ToString();
    }

    public async Task<int> DeleteOrphanEpisodesAsync(CancellationToken ct)
    {
        const string sql =
            """
            DELETE FROM imdb_import_episodes
            WHERE tconst NOT IN (SELECT tconst FROM imdb_imports);
            """;
        try
        {
            return await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting orphan rows from imdb_import_episodes");
            throw;
        }
    }

    private static string BuildEpisodesInsertSql(List<ImdbEpisodeTsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INSERT INTO imdb_import_episodes (tconst, parent_tconst, season_number, episode_number)");
        sb.AppendLine("VALUES");

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var comma = i < rows.Count - 1 ? "," : "";

            sb.AppendLine($"    ('" + EscapeSql(row.Tconst) + "', '" + EscapeSql(row.ParentTconst) + "', " + row.SeasonNumber + ", " + row.EpisodeNumber + ")" + comma);
        }

        sb.AppendLine("ON CONFLICT (tconst) DO NOTHING");

        return sb.ToString();
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''");
    }

    private static string BoolToSql(bool value)
    {
        return value ? "TRUE" : "FALSE";
    }
}
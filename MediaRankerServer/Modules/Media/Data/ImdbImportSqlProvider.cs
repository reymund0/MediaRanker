
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

    public async Task<int> DeleteTvPilotImportsAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.Database.ExecuteSqlRawAsync("""
                DELETE FROM imdb_imports
                WHERE title_type = 'tvPilot';
                """, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting unwanted rows from imdb_imports");
            throw;
        }
    }

    public async Task<int> DeleteFutureImportsAsync(CancellationToken ct)
    {
        var currentYear = DateTime.Now.Year;
        try
        {
            return await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM imdb_imports
                WHERE start_year > {currentYear};
                """, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting future rows from imdb_imports");
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

    public async Task<int> DeleteOrphanEpisodesAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.Database.ExecuteSqlRawAsync("""
                DELETE FROM imdb_import_episodes e
                WHERE NOT EXISTS (
                    SELECT 1 FROM imdb_imports i WHERE i.tconst = e.tconst
                );
                """, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting orphan rows from imdb_import_episodes");
            throw;
        }
    }

    private static string BuildBasicsInsertSql(List<ImdbTsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INSERT INTO imdb_imports (tconst, title_type, primary_title, original_title, is_adult, start_year, end_year, runtime_minutes, genres, raw_line)");
        sb.AppendLine("VALUES");

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var comma = i < rows.Count - 1 ? "," : "";

            var startYear = row.StartYear.HasValue ? row.StartYear.Value.ToString() : "NULL";
            var endYear = row.EndYear.HasValue ? row.EndYear.Value.ToString() : "NULL";
            var runtime = row.RuntimeMinutes.HasValue ? row.RuntimeMinutes.Value.ToString() : "NULL";
            var genres = row.Genres != null ? "'" + EscapeSql(row.Genres) + "'" : "NULL";

            sb.AppendLine($"    ('" + EscapeSql(row.Tconst) + "', '" + EscapeSql(row.TitleType) + "', '" + EscapeSql(row.PrimaryTitle) + "', '" + EscapeSql(row.OriginalTitle) + "', " + BoolToSql(row.IsAdult) + ", " + startYear + ", " + endYear + ", " + runtime + ", " + genres + ", '" + EscapeSql(row.RawLine) + "')" + comma);
        }

        sb.Append("""
            ON CONFLICT (tconst) DO UPDATE SET
                title_type       = EXCLUDED.title_type,
                primary_title    = EXCLUDED.primary_title,
                original_title   = EXCLUDED.original_title,
                is_adult         = EXCLUDED.is_adult,
                start_year       = EXCLUDED.start_year,
                end_year         = EXCLUDED.end_year,
                runtime_minutes  = EXCLUDED.runtime_minutes,
                genres           = EXCLUDED.genres,
                raw_line         = EXCLUDED.raw_line
            """);

        return sb.ToString();
    }

    private static string BuildEpisodesInsertSql(List<ImdbEpisodeTsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INSERT INTO imdb_import_episodes (tconst, parent_tconst, season_number, episode_number, raw_line)");
        sb.AppendLine("VALUES");

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var comma = i < rows.Count - 1 ? "," : "";

            sb.AppendLine($"    ('" + EscapeSql(row.Tconst) + "', '" + EscapeSql(row.ParentTconst) + "', " + row.SeasonNumber + ", " + row.EpisodeNumber + ", '" + EscapeSql(row.RawLine) + "')" + comma);
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
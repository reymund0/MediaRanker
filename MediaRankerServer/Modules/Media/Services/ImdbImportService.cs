using MediaRankerServer.Shared.Data;
using MediaRankerServer.Modules.Media.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MediaRankerServer.Modules.Media.Services;

public record ImdbImportResult(int Inserted, int Skipped);

public class ImdbImportService(
    ImdbTsvProvider parser,
    PostgreSQLContext dbContext,
    ILogger<ImdbImportService> logger)
{
    int totalInserted;
    int totalSkipped;
    
    public async Task<ImdbImportResult> ImportAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB import job run.");

        totalInserted = 0;
        totalSkipped = 0;

        await parser.RunBatchImportAsync(ImportBatchAsync, ct);

        logger.LogInformation("IMDB import run completed. Total inserted: {Inserted}, Total skipped (duplicates): {Skipped}",
            totalInserted, totalSkipped);

        return new ImdbImportResult(totalInserted, totalSkipped);
    }

    private async Task ImportBatchAsync(List<ImdbTsvRow> rows, CancellationToken cancellationToken)
    {
        string sql = string.Empty;
        try {
            sql = BuildInsertSql(rows);
            var result = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            var inserted = result;
            var skipped = rows.Count - inserted;
            totalInserted += inserted;
            totalSkipped += skipped;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Error importing batch of IMDB rows. SQL: {Sql}", sql);
        }
    }

    private static string BuildInsertSql(List<ImdbTsvRow> rows)
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
            var genres = row.Genres != null ? $"'{EscapeSql(row.Genres)}'" : "NULL";

            sb.AppendLine($"    ('{EscapeSql(row.Tconst)}', '{EscapeSql(row.TitleType)}', '{EscapeSql(row.PrimaryTitle)}', '{EscapeSql(row.OriginalTitle)}', {BoolToSql(row.IsAdult)}, {startYear}, {endYear}, {runtime}, {genres}){comma}");
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

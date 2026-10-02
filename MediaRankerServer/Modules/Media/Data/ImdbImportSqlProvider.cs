using System.Globalization;
using System.Text;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace MediaRankerServer.Modules.Media.Data;

public class ImdbImportSqlProvider(
    PostgreSQLContext dbContext,
    ILogger<ImdbImportSqlProvider> logger,
    IOptions<ImdbImportOptions> options) : IImdbImportProvider
{
    private readonly int defaultCleanupRows = Math.Max(1, options.Value.MaxCleanupRowsPerUnit);
    private readonly TimeSpan statementTimeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.MaxStatementSeconds));

    public ImdbImportSqlProvider(PostgreSQLContext dbContext, ILogger<ImdbImportSqlProvider> logger)
        : this(dbContext, logger, Options.Create(new ImdbImportOptions()))
    {
    }

    public async Task<ImdbImportResult> ImportBasicsAsync(List<ImdbTsvRow> rows, CancellationToken ct)
    {
        var acceptedRows = rows.Where(row => row.TitleType != "videoGame").ToList();
        if (acceptedRows.Count == 0) return new ImdbImportResult(0, rows.Count);

        var affected = await ExecuteSqlAsync(
            token => dbContext.Database.ExecuteSqlRawAsync(BuildBasicsInsertSql(acceptedRows), token),
            "basics", rows.Count, ct);
        return new ImdbImportResult(affected, rows.Count - affected);
    }

    public async Task<ImdbImportResult> ImportEpisodesAsync(List<ImdbEpisodeTsvRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return new ImdbImportResult(0, 0);
        var affected = await ExecuteSqlAsync(
            token => dbContext.Database.ExecuteSqlRawAsync(BuildEpisodesInsertSql(rows), token),
            "episodes", rows.Count, ct);
        return new ImdbImportResult(affected, rows.Count - affected);
    }

    public async Task<ImdbImportResult> ImportRatingsAsync(List<ImdbRatingTsvRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return new ImdbImportResult(0, 0);
        var affected = await ExecuteSqlAsync(
            token => dbContext.Database.ExecuteSqlRawAsync(BuildRatingsInsertSql(rows), token),
            "ratings", rows.Count, ct);
        return new ImdbImportResult(affected, rows.Count - affected);
    }

    public async Task<DateTimeOffset> GetDatabaseUtcNowAsync(CancellationToken ct)
    {
        try
        {
            dbContext.Database.SetCommandTimeout(statementTimeout);
            return await dbContext.Database.SqlQueryRaw<DateTimeOffset>("SELECT now() AS \"Value\"").SingleAsync(ct);
        }
        catch (Exception ex)
        {
            LogFailure("database-clock", 0, ex);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    public Task<int> DeleteFutureImportsAsync(CancellationToken ct) => DeleteFutureImportsAsync(defaultCleanupRows, ct);
    public Task<int> DeleteTvPilotImportsAsync(CancellationToken ct) => DeleteTvPilotImportsAsync(defaultCleanupRows, ct);
    public Task<int> DeleteOrphanEpisodesAsync(CancellationToken ct) => DeleteOrphanEpisodesAsync(defaultCleanupRows, ct);
    public Task<int> DeleteStaleRatingsAsync(DateTimeOffset cutoffUtc, CancellationToken ct) => DeleteStaleRatingsAsync(cutoffUtc, defaultCleanupRows, ct);

    public Task<int> DeleteTvPilotImportsAsync(int maxRows, CancellationToken ct) =>
        ExecuteBoundedDeleteAsync("imdb_imports", "title_type = 'tvPilot'", maxRows, "tv-pilot", ct);

    public Task<int> DeleteFutureImportsAsync(int maxRows, CancellationToken ct) =>
        ExecuteBoundedDeleteAsync("imdb_imports", "start_year > EXTRACT(YEAR FROM CURRENT_DATE)", maxRows, "future", ct);

    public Task<int> DeleteOrphanEpisodesAsync(int maxRows, CancellationToken ct) =>
        ExecuteBoundedDeleteAsync("imdb_import_episodes", "NOT EXISTS (SELECT 1 FROM imdb_imports i WHERE i.tconst = imdb_import_episodes.tconst)", maxRows, "orphan-episodes", ct);

    public async Task<ImdbCleanupBatchResult> DeleteOrphanEpisodesBatchAsync(long? afterId, int maxRows, CancellationToken ct)
    {
        ValidateLimit(maxRows);
        const string sql = """
            WITH candidates AS MATERIALIZED (
                SELECT e.id, e.tconst
                FROM imdb_import_episodes e
                WHERE (@after_id IS NULL OR e.id > @after_id)
                ORDER BY e.id
                LIMIT @max_rows
            ), orphan_candidates AS (
                SELECT c.id
                FROM candidates c
                WHERE NOT EXISTS (SELECT 1 FROM imdb_imports i WHERE i.tconst = c.tconst)
            ), deleted AS (
                DELETE FROM imdb_import_episodes e
                USING orphan_candidates o
                WHERE e.id = o.id
                RETURNING e.id
            )
            SELECT (SELECT count(*)::integer FROM deleted) AS affected,
                   (SELECT max(id)::bigint FROM candidates) AS next_id,
                   ((SELECT count(*)::integer FROM candidates) = @max_rows) AS has_more;
            """;

        try
        {
            dbContext.Database.SetCommandTimeout(statementTimeout);
            var pages = await dbContext.Database.SqlQueryRaw<ImdbCleanupBatchPage>(
                sql,
                new NpgsqlParameter("after_id", NpgsqlDbType.Bigint) { Value = afterId is null ? DBNull.Value : afterId.Value },
                new NpgsqlParameter("max_rows", NpgsqlDbType.Integer) { Value = maxRows })
                .ToListAsync(ct);
            var page = pages.Single();
            var result = new ImdbCleanupBatchResult(page.Affected, page.NextId, page.HasMore);
            logger.LogInformation("IMDb cleanup stage {Stage} examined through {NextId}, affected {Count} rows.",
                "orphan-episodes", result.NextId, result.Affected);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError("IMDb SQL stage {Stage} failed for candidate cap {CandidateCap}. ErrorCategory: {ErrorCategory}.",
                "orphan-episodes", maxRows, ex.GetType().Name);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    public async Task<int> DeleteStaleRatingsAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct)
    {
        ValidateLimit(maxRows);
        var affected = await ExecuteSqlAsync(
            token => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH targets AS (
                    SELECT id FROM imdb_import_ratings
                    WHERE updated_at < {cutoffUtc}
                    ORDER BY id
                    LIMIT {maxRows}
                )
                DELETE FROM imdb_import_ratings r
                USING targets t
                WHERE r.id = t.id
                  AND r.updated_at < {cutoffUtc};
                """, token), "stale-ratings", maxRows, ct);
        logger.LogInformation("IMDb cleanup stage {Stage} affected {Count} rows.", "stale-ratings", affected);
        return affected;
    }

    private async Task<int> ExecuteBoundedDeleteAsync(
        string table,
        string predicate,
        int maxRows,
        string stage,
        CancellationToken ct)
    {
        ValidateLimit(maxRows);
        // Table and predicates are constants owned by this class; no provider values are interpolated here.
        var sql = $"""
            WITH targets AS (
                SELECT id FROM {table}
                WHERE {predicate}
                ORDER BY id
                LIMIT {maxRows}
            )
            DELETE FROM {table} target
            USING targets t
            WHERE target.id = t.id;
            """;
        var affected = await ExecuteSqlAsync(
            token => dbContext.Database.ExecuteSqlRawAsync(sql, token), stage, maxRows, ct);
        logger.LogInformation("IMDb cleanup stage {Stage} affected {Count} rows.", stage, affected);
        return affected;
    }

    private async Task<int> ExecuteSqlAsync(
        Func<CancellationToken, Task<int>> execute,
        string stage,
        int rows,
        CancellationToken ct)
    {
        try
        {
            dbContext.Database.SetCommandTimeout(statementTimeout);
            return await execute(ct);
        }
        catch (Exception ex)
        {
            LogFailure(stage, rows, ex);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    private static string BuildBasicsInsertSql(List<ImdbTsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("INSERT INTO imdb_imports (tconst, title_type, primary_title, original_title, is_adult, start_year, end_year, runtime_minutes, genres, raw_line)");
        sb.AppendLine("VALUES");
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var comma = i < rows.Count - 1 ? "," : string.Empty;
            sb.AppendLine($"    ('{EscapeSql(row.Tconst)}', '{EscapeSql(row.TitleType)}', '{EscapeSql(row.PrimaryTitle)}', '{EscapeSql(row.OriginalTitle)}', {BoolToSql(row.IsAdult)}, {NullableInt(row.StartYear)}, {NullableInt(row.EndYear)}, {NullableInt(row.RuntimeMinutes)}, {NullableString(row.Genres)}, '{EscapeSql(row.RawLine)}'){comma}");
        }
        sb.Append("ON CONFLICT (tconst) DO UPDATE SET title_type = EXCLUDED.title_type, primary_title = EXCLUDED.primary_title, original_title = EXCLUDED.original_title, is_adult = EXCLUDED.is_adult, start_year = EXCLUDED.start_year, end_year = EXCLUDED.end_year, runtime_minutes = EXCLUDED.runtime_minutes, genres = EXCLUDED.genres, raw_line = EXCLUDED.raw_line");
        return sb.ToString();
    }

    private static string BuildEpisodesInsertSql(List<ImdbEpisodeTsvRow> rows)
    {
        var sb = new StringBuilder("INSERT INTO imdb_import_episodes (tconst, parent_tconst, season_number, episode_number, raw_line) VALUES\n");
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            sb.Append($"('{EscapeSql(row.Tconst)}', '{EscapeSql(row.ParentTconst)}', {row.SeasonNumber}, {row.EpisodeNumber}, '{EscapeSql(row.RawLine)}')");
            sb.AppendLine(i == rows.Count - 1 ? string.Empty : ",");
        }
        sb.Append("ON CONFLICT (tconst) DO UPDATE SET parent_tconst = EXCLUDED.parent_tconst, season_number = EXCLUDED.season_number, episode_number = EXCLUDED.episode_number, raw_line = EXCLUDED.raw_line, updated_at = now()");
        return sb.ToString();
    }

    private static string BuildRatingsInsertSql(List<ImdbRatingTsvRow> rows)
    {
        var sb = new StringBuilder("INSERT INTO imdb_import_ratings (tconst, average_rating, num_votes) VALUES\n");
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            sb.Append($"('{EscapeSql(row.Tconst)}', {row.AverageRating.ToString(CultureInfo.InvariantCulture)}, {row.NumVotes})");
            sb.AppendLine(i == rows.Count - 1 ? string.Empty : ",");
        }
        sb.Append("ON CONFLICT (tconst) DO UPDATE SET average_rating = EXCLUDED.average_rating, num_votes = EXCLUDED.num_votes, updated_at = now()");
        return sb.ToString();
    }

    private void LogFailure(string stage, int rows, Exception ex) =>
        logger.LogError("IMDb SQL stage {Stage} failed for {Rows} rows. ErrorCategory: {ErrorCategory}.", stage, rows, ex.GetType().Name);

    private static void ValidateLimit(int maxRows)
    {
        if (maxRows <= 0) throw new ArgumentOutOfRangeException(nameof(maxRows));
    }

    private sealed class ImdbCleanupBatchPage
    {
        public int Affected { get; set; }
        public long? NextId { get; set; }
        public bool HasMore { get; set; }
    }

    private static string NullableInt(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";
    private static string NullableString(string? value) => value is null ? "NULL" : $"'{EscapeSql(value)}'";
    // ExecuteSqlRaw also parses composite-format braces, including those inside SQL literals.
    // Escape both layers so retained source text reaches PostgreSQL unchanged.
    private static string EscapeSql(string value) => value.Replace("'", "''", StringComparison.Ordinal)
        .Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
    private static string BoolToSql(bool value) => value ? "TRUE" : "FALSE";
}

using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace MediaRankerServer.Modules.Media.Data;

/// <summary>Executes deterministic keyset load units; a season group is never split across units.</summary>
public class ImdbLoadSqlProvider(
    PostgreSQLContext dbContext,
    ILogger<ImdbLoadSqlProvider> logger,
    IOptions<ImdbImportOptions> options) : IImdbLoadProvider
{
    private readonly ImdbImportOptions config = options.Value;

    public ImdbLoadSqlProvider(PostgreSQLContext dbContext, ILogger<ImdbLoadSqlProvider> logger)
        : this(dbContext, logger, Options.Create(new ImdbImportOptions()))
    {
    }

    public Task<ImdbLoadResult> LoadNonSeriesMediaAsync(int minVotesMovies, CancellationToken ct) =>
        DrainAsync((after, token) => LoadNonSeriesMediaBatchAsync(minVotesMovies, after, config.MaxLoadRowsPerUnit, token), ct);

    public Task<ImdbLoadResult> LoadSeriesCollectionsAsync(int minVotesTv, CancellationToken ct) =>
        DrainAsync((after, token) => LoadSeriesCollectionsBatchAsync(minVotesTv, after, config.MaxLoadRowsPerUnit, token), ct);

    public async Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct)
    {
        var total = 0;
        string? parent = null;
        int? season = null;
        while (true)
        {
            var batch = await LoadSeasonCollectionsBatchAsync(parent, season, config.MaxLoadRowsPerUnit, ct);
            total += batch.Affected;
            if (!batch.HasMore) return new ImdbLoadResult(total);
            parent = batch.NextParentTconst;
            season = batch.NextSeasonNumber;
        }
    }

    public Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct) =>
        DrainAsync((after, token) => LoadEpisodeMediaBatchAsync(after, config.MaxLoadRowsPerUnit, token), ct);

    public Task<ImdbLoadBatchResult> LoadNonSeriesMediaBatchAsync(int minVotesMovies, string? afterTconst, int maxRows, CancellationToken ct)
    {
        ValidateLimit(maxRows);
        var after = CursorPredicate("i.tconst", afterTconst);
        var sql = $"""
            WITH selected AS (
                SELECT i.primary_title, i.start_year, i.tconst, i.title_type
                FROM imdb_imports i
                INNER JOIN imdb_import_ratings r ON r.tconst = i.tconst AND r.num_votes >= {minVotesMovies}
                WHERE i.title_type IN ('movie', 'tvMovie', 'short', 'tvShort', 'video')
                  AND {after}
                ORDER BY i.tconst
                LIMIT {maxRows}
            ), upsert AS (
                INSERT INTO media (title, release_date, external_id, external_source, media_type, created_at, updated_at)
                SELECT primary_title,
                       CASE WHEN start_year IS NULL THEN NULL ELSE make_date(start_year, 7, 1) END,
                       tconst, '{nameof(MediaExternalSource.Imdb)}', 'Movie', now(), now()
                FROM selected
                ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL
                DO UPDATE SET title = EXCLUDED.title, release_date = EXCLUDED.release_date,
                              media_type = EXCLUDED.media_type, updated_at = now()
                RETURNING external_id
            ) SELECT external_id AS "Value" FROM upsert ORDER BY external_id;
            """;
        return ExecuteKeysetAsync(sql, "non-series", maxRows, ct);
    }

    public Task<ImdbLoadBatchResult> LoadSeriesCollectionsBatchAsync(int minVotesTv, string? afterTconst, int maxRows, CancellationToken ct)
    {
        ValidateLimit(maxRows);
        var after = CursorPredicate("i.tconst", afterTconst);
        var sql = $"""
            WITH selected AS (
                SELECT i.primary_title, i.start_year, i.tconst
                FROM imdb_imports i
                INNER JOIN imdb_import_ratings r ON r.tconst = i.tconst AND r.num_votes >= {minVotesTv}
                WHERE i.title_type IN ('tvSeries', 'tvMiniSeries')
                  AND {after}
                ORDER BY i.tconst
                LIMIT {maxRows}
            ), upsert AS (
                INSERT INTO media_collections
                    (title, release_date, external_id, external_source, collection_type,
                     media_type, parent_media_collection_id, created_at, updated_at)
                SELECT primary_title,
                       CASE WHEN start_year IS NULL THEN NULL ELSE make_date(start_year, 7, 1) END,
                       tconst, '{nameof(MediaExternalSource.Imdb)}', 'Series', 'TvShow', NULL, now(), now()
                FROM selected
                ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL AND collection_type = 'Series'
                DO UPDATE SET title = EXCLUDED.title, release_date = EXCLUDED.release_date,
                              media_type = EXCLUDED.media_type, updated_at = now()
                RETURNING external_id
            ) SELECT external_id AS "Value" FROM upsert ORDER BY external_id;
            """;
        return ExecuteKeysetAsync(sql, "series", maxRows, ct);
    }

    public async Task<ImdbSeasonLoadBatchResult> LoadSeasonCollectionsBatchAsync(string? afterParentTconst, int? afterSeasonNumber, int maxGroups, CancellationToken ct)
    {
        ValidateLimit(maxGroups);
        var after = SeasonCursorPredicate(afterParentTconst, afterSeasonNumber);
        var sql = $"""
            WITH groups AS (
                SELECT e.parent_tconst, e.season_number, MIN(i.start_year) AS season_start_year,
                       mc.id AS parent_id
                FROM imdb_import_episodes e
                INNER JOIN imdb_imports i ON i.tconst = e.tconst
                INNER JOIN media_collections mc ON mc.external_id = e.parent_tconst
                                                AND mc.external_source = '{nameof(MediaExternalSource.Imdb)}'
                                                AND mc.collection_type = 'Series'
                WHERE e.season_number <> -1
                  AND {after}
                GROUP BY e.parent_tconst, e.season_number, mc.id
                ORDER BY e.parent_tconst, e.season_number
                LIMIT {maxGroups}
            ), upsert AS (
                INSERT INTO media_collections
                    (title, release_date, external_id, external_source, collection_type,
                     media_type, parent_media_collection_id, season_number, created_at, updated_at)
                SELECT season_number::text,
                       CASE WHEN season_start_year IS NULL THEN NULL ELSE make_date(season_start_year, 7, 1) END,
                       parent_tconst, '{nameof(MediaExternalSource.Imdb)}', 'Season', 'TvShow', parent_id, season_number, now(), now()
                FROM groups
                ON CONFLICT (title, collection_type, media_type, parent_media_collection_id)
                    WHERE parent_media_collection_id IS NOT NULL
                DO UPDATE SET release_date = EXCLUDED.release_date, external_id = EXCLUDED.external_id,
                              external_source = EXCLUDED.external_source, season_number = EXCLUDED.season_number,
                              updated_at = now()
                RETURNING external_id || E'\t' || title AS "Value"
            )
            SELECT "Value" FROM upsert
            ORDER BY split_part("Value", E'\t', 1),
                     CASE WHEN split_part("Value", E'\t', 2) = 'Unknown' THEN -1
                          ELSE split_part("Value", E'\t', 2)::integer END;
            """;
        var keys = await ExecuteKeysAsync(sql, "seasons", maxGroups, ct);
        if (keys.Count == 0) return new ImdbSeasonLoadBatchResult(0, afterParentTconst, afterSeasonNumber, false);
        var key = ParseSeasonKey(keys[^1]);
        return new ImdbSeasonLoadBatchResult(keys.Count, key.Parent, key.Season, keys.Count == maxGroups);
    }

    public Task<ImdbLoadBatchResult> LoadEpisodeMediaBatchAsync(string? afterTconst, int maxRows, CancellationToken ct)
    {
        ValidateLimit(maxRows);
        var after = CursorPredicate("i.tconst", afterTconst);
        var sql = $"""
            WITH candidates AS MATERIALIZED (
                SELECT i.primary_title, i.start_year, i.tconst
                FROM imdb_imports i
                WHERE i.title_type = 'tvEpisode' AND {after}
                ORDER BY i.tconst
                LIMIT {maxRows}
            ), selected AS (
                SELECT candidates.primary_title, candidates.start_year, candidates.tconst,
                       season.id AS season_id, e.episode_number
                FROM candidates
                INNER JOIN imdb_import_episodes e ON e.tconst = candidates.tconst
                                                 AND e.season_number <> -1
                INNER JOIN media_collections series ON series.external_id = e.parent_tconst
                                                    AND series.external_source = '{nameof(MediaExternalSource.Imdb)}'
                                                    AND series.collection_type = 'Series'
                INNER JOIN media_collections season ON season.parent_media_collection_id = series.id
                                                    AND season.collection_type = 'Season'
                                                    AND season.media_type = 'TvShow'
                                                    AND season.season_number = e.season_number
                                                    AND season.title = e.season_number::text
            ), upsert AS (
                INSERT INTO media (title, release_date, external_id, external_source,
                                   media_type, media_collection_id, episode_number, created_at, updated_at)
                SELECT s.primary_title,
                       CASE WHEN s.start_year IS NULL THEN NULL ELSE make_date(s.start_year, 7, 1) END,
                       s.tconst, '{nameof(MediaExternalSource.Imdb)}', 'TvShow', s.season_id,
                       CASE WHEN s.episode_number >= 0 THEN s.episode_number ELSE NULL END, now(), now()
                FROM selected s
                ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL
                DO UPDATE SET title = EXCLUDED.title, release_date = EXCLUDED.release_date,
                              media_type = EXCLUDED.media_type, media_collection_id = EXCLUDED.media_collection_id,
                              episode_number = EXCLUDED.episode_number,
                              updated_at = now()
                RETURNING external_id
            ), scanned AS (
                SELECT count(*)::integer AS candidate_count, max(tconst) AS next_key
                FROM candidates
            ), affected AS (
                SELECT count(*)::integer AS affected_count FROM upsert
            )
            SELECT affected.affected_count AS affected,
                   scanned.next_key AS next_key,
                   (scanned.candidate_count = {maxRows}) AS has_more
            FROM scanned CROSS JOIN affected;
            """;
        return ExecuteEpisodeBatchAsync(sql, maxRows, ct);
    }

    public async Task<ImdbUnknownEpisodeCleanupBatchResult> DeleteUnknownSeasonEpisodesBatchAsync(
        long? afterMediaId, int maxRows, CancellationToken ct)
    {
        ValidateLimit(maxRows);
        const string sql = """
            WITH candidates AS MATERIALIZED (
                SELECT m.id
                FROM media m
                INNER JOIN media_collections season ON season.id = m.media_collection_id
                INNER JOIN media_collections series ON series.id = season.parent_media_collection_id
                WHERE (@after_media_id IS NULL OR m.id > @after_media_id)
                  AND m.external_source = 'Imdb'
                  AND m.media_type = 'TvShow'
                  AND season.collection_type = 'Season'
                  AND season.media_type = 'TvShow'
                  AND season.season_number IS NULL
                  AND season.external_source = 'Imdb'
                  AND series.collection_type = 'Series'
                  AND series.media_type = 'TvShow'
                  AND series.external_source = 'Imdb'
                ORDER BY m.id
                LIMIT @max_rows
            ), deleted AS (
                DELETE FROM media m
                USING candidates c
                WHERE m.id = c.id
                  AND NOT EXISTS (SELECT 1 FROM reviews r WHERE r.media_id = m.id)
                RETURNING m.id
            )
            SELECT COUNT(c.id)::integer AS candidate_count,
                   MAX(c.id)::bigint AS next_id,
                   (COUNT(c.id) = @max_rows) AS has_more,
                   (SELECT COUNT(*)::integer FROM deleted) AS deleted,
                   (COUNT(c.id)::integer - (SELECT COUNT(*)::integer FROM deleted)) AS skipped
            FROM candidates c;
            """;

        var timeout = dbContext.Database.GetCommandTimeout();
        try
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(config.MaxStatementSeconds));
            var pages = await dbContext.Database.SqlQueryRaw<UnknownEpisodeCleanupBatchPage>(
                sql,
                new NpgsqlParameter("after_media_id", NpgsqlDbType.Bigint) { Value = afterMediaId is null ? DBNull.Value : afterMediaId.Value },
                new NpgsqlParameter("max_rows", NpgsqlDbType.Integer) { Value = maxRows })
                .ToListAsync(ct);
            var page = pages.Single();
            var result = new ImdbUnknownEpisodeCleanupBatchResult(page.Deleted, page.Skipped, page.NextId, page.HasMore);
            logger.LogInformation(
                "IMDb cleanup stage {Stage} committed through media ID {NextId}; deleted {Deleted} and skipped {Skipped} reviewed episodes.",
                "unknown-season-episodes", result.NextMediaId, result.Deleted, result.Skipped);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError("IMDb cleanup stage {Stage} failed at candidate cap {CandidateCap}. ErrorCategory: {ErrorCategory}.",
                "unknown-season-episodes", maxRows, ex.GetType().Name);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(timeout);
        }
    }

    public async Task<ImdbUnknownSeasonCleanupBatchResult> DeleteEmptyUnknownSeasonsBatchAsync(
        long? afterSeasonId, int maxRows, CancellationToken ct)
    {
        ValidateLimit(maxRows);
        const string sql = """
            WITH candidates AS MATERIALIZED (
                SELECT season.id, season.parent_media_collection_id
                FROM media_collections season
                INNER JOIN media_collections series ON series.id = season.parent_media_collection_id
                WHERE (@after_season_id IS NULL OR season.id > @after_season_id)
                  AND season.collection_type = 'Season'
                  AND season.season_number IS NULL
                  AND season.media_type = 'TvShow'
                  AND season.external_source = 'Imdb'
                  AND series.collection_type = 'Series'
                  AND series.media_type = 'TvShow'
                  AND series.external_source = 'Imdb'
                  AND NOT EXISTS (SELECT 1 FROM media m WHERE m.media_collection_id = season.id)
                  AND NOT EXISTS (SELECT 1 FROM media_collections child WHERE child.parent_media_collection_id = season.id)
                ORDER BY season.id
                LIMIT @max_rows
            ), deleted AS (
                DELETE FROM media_collections season
                USING candidates c
                WHERE season.id = c.id
                  AND NOT EXISTS (SELECT 1 FROM media m WHERE m.media_collection_id = season.id)
                  AND NOT EXISTS (SELECT 1 FROM media_collections child WHERE child.parent_media_collection_id = season.id)
                RETURNING season.id, season.parent_media_collection_id
            )
            SELECT COUNT(c.id)::integer AS candidate_count,
                   MAX(c.id)::bigint AS next_id,
                   (COUNT(c.id) = @max_rows) AS has_more,
                   COUNT(d.id)::integer AS deleted_seasons,
                   COALESCE(array_agg(DISTINCT d.parent_media_collection_id)
                       FILTER (WHERE d.parent_media_collection_id IS NOT NULL), ARRAY[]::bigint[]) AS parent_ids
            FROM candidates c
            LEFT JOIN deleted d ON d.id = c.id;
            """;
        const string deleteSeriesSql = """
            WITH parents AS MATERIALIZED (
                SELECT series.id,
                       EXISTS (SELECT 1 FROM reviews r WHERE r.media_collection_id = series.id) AS has_review
                FROM media_collections series
                WHERE series.id = ANY(@parent_ids)
                  AND series.collection_type = 'Series'
                  AND series.media_type = 'TvShow'
                  AND series.external_source = 'Imdb'
                  AND NOT EXISTS (SELECT 1 FROM media_collections child WHERE child.parent_media_collection_id = series.id)
                  AND NOT EXISTS (SELECT 1 FROM media m WHERE m.media_collection_id = series.id)
            ), deleted AS (
                DELETE FROM media_collections series
                USING parents p
                WHERE series.id = p.id AND NOT p.has_review
                RETURNING series.id
            )
            SELECT COUNT(d.id)::integer AS deleted,
                   COUNT(p.id) FILTER (WHERE p.has_review)::integer AS skipped_reviewed
            FROM parents p
            LEFT JOIN deleted d ON d.id = p.id;
            """;

        var timeout = dbContext.Database.GetCommandTimeout();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(config.MaxStatementSeconds));
            var pages = await dbContext.Database.SqlQueryRaw<UnknownSeasonCleanupBatchPage>(
                sql,
                new NpgsqlParameter("after_season_id", NpgsqlDbType.Bigint) { Value = afterSeasonId is null ? DBNull.Value : afterSeasonId.Value },
                new NpgsqlParameter("max_rows", NpgsqlDbType.Integer) { Value = maxRows })
                .ToListAsync(ct);
            var page = pages.Single();
            var seriesPage = page.ParentIds.Length == 0
                ? new UnknownSeriesCleanupPage()
                : (await dbContext.Database.SqlQueryRaw<UnknownSeriesCleanupPage>(
                    deleteSeriesSql,
                    new NpgsqlParameter("parent_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = page.ParentIds })
                    .ToListAsync(ct)).Single();
            await transaction.CommitAsync(ct);

            var result = new ImdbUnknownSeasonCleanupBatchResult(
                page.DeletedSeasons, seriesPage.Deleted, seriesPage.SkippedReviewed, page.NextId, page.HasMore);
            logger.LogInformation(
                "IMDb cleanup stage {Stage} committed through season ID {NextId}; deleted {Seasons} seasons and {Series} series; skipped {Skipped} reviewed series.",
                "unknown-seasons", result.NextSeasonId, result.DeletedSeasons, result.DeletedSeries, result.SkippedReviewedSeries);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError("IMDb cleanup stage {Stage} failed at candidate cap {CandidateCap}. ErrorCategory: {ErrorCategory}.",
                "unknown-seasons", maxRows, ex.GetType().Name);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(timeout);
        }
    }

    private async Task<ImdbLoadBatchResult> ExecuteEpisodeBatchAsync(string sql, int maxRows, CancellationToken ct)
    {
        try
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(config.MaxStatementSeconds));
            // Materialize the DML CTE query directly; further EF composition can wrap it in an outer SELECT.
            var pages = await dbContext.Database.SqlQueryRaw<EpisodeLoadBatchPage>(sql).ToListAsync(ct);
            var page = pages.Single();
            return new ImdbLoadBatchResult(page.Affected, page.NextKey, page.HasMore);
        }
        catch (Exception ex)
        {
            logger.LogError("IMDb load stage {Stage} failed at unit cap {UnitCap}. ErrorCategory: {ErrorCategory}.",
                "episodes", maxRows, ex.GetType().Name);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    private async Task<ImdbLoadBatchResult> ExecuteKeysetAsync(string sql, string stage, int maxRows, CancellationToken ct)
    {
        var keys = await ExecuteKeysAsync(sql, stage, maxRows, ct);
        var next = keys.Count == 0 ? null : keys[^1];
        return new ImdbLoadBatchResult(keys.Count, next, keys.Count == maxRows);
    }

    private async Task<List<string>> ExecuteKeysAsync(string sql, string stage, int maxRows, CancellationToken ct)
    {
        try
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(config.MaxStatementSeconds));
            return await dbContext.Database.SqlQueryRaw<string>(sql).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError("IMDb load stage {Stage} failed at unit cap {UnitCap}. ErrorCategory: {ErrorCategory}.", stage, maxRows, ex.GetType().Name);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    private async Task<ImdbLoadResult> DrainAsync(
        Func<string?, CancellationToken, Task<ImdbLoadBatchResult>> operation,
        CancellationToken ct)
    {
        var total = 0;
        string? after = null;
        while (true)
        {
            var batch = await operation(after, ct);
            total += batch.Affected;
            if (!batch.HasMore) return new ImdbLoadResult(total);
            after = batch.NextKey;
        }
    }

    private static string CursorPredicate(string column, string? after) =>
        after is null ? "TRUE" : $"{column} > '{Escape(after)}'";

    private static string SeasonCursorPredicate(string? parent, int? season) => parent is null
        ? "TRUE"
        : $"(e.parent_tconst > '{Escape(parent)}' OR (e.parent_tconst = '{Escape(parent)}' AND e.season_number > {season ?? int.MinValue}))";

    private static (string Parent, int Season) ParseSeasonKey(string value)
    {
        var separator = value.LastIndexOf('\t');
        if (separator <= 0)
            throw new InvalidDataException("IMDb season load returned an invalid progress key.");
        var title = value[(separator + 1)..];
        if (title == "Unknown") return (value[..separator], -1);
        if (!int.TryParse(title, out var season))
            throw new InvalidDataException("IMDb season load returned an invalid progress key.");
        return (value[..separator], season);
    }

    private static void ValidateLimit(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private sealed class EpisodeLoadBatchPage
    {
        public int Affected { get; set; }
        public string? NextKey { get; set; }
        public bool HasMore { get; set; }
    }

    private sealed class UnknownEpisodeCleanupBatchPage
    {
        public int Deleted { get; set; }
        public int Skipped { get; set; }
        public long? NextId { get; set; }
        public bool HasMore { get; set; }
    }

    private sealed class UnknownSeasonCleanupBatchPage
    {
        public int DeletedSeasons { get; set; }
        public long? NextId { get; set; }
        public bool HasMore { get; set; }
        public long[] ParentIds { get; set; } = [];
    }

    private sealed class UnknownSeriesCleanupPage
    {
        public int Deleted { get; set; }
        public int SkippedReviewed { get; set; }
    }
}

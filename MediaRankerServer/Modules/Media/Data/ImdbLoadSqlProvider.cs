using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Jobs;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
                INSERT INTO media (title, release_date, external_id, external_source, media_type_id, created_at, updated_at)
                SELECT primary_title,
                       CASE WHEN start_year IS NULL THEN NULL ELSE make_date(start_year, 7, 1) END,
                       tconst, '{nameof(MediaExternalSource.Imdb)}', -3, now(), now()
                FROM selected
                ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL
                DO UPDATE SET title = EXCLUDED.title, release_date = EXCLUDED.release_date,
                              media_type_id = EXCLUDED.media_type_id, updated_at = now()
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
                     media_type_id, parent_media_collection_id, created_at, updated_at)
                SELECT primary_title,
                       CASE WHEN start_year IS NULL THEN NULL ELSE make_date(start_year, 7, 1) END,
                       tconst, '{nameof(MediaExternalSource.Imdb)}', 'Series', -4, NULL, now(), now()
                FROM selected
                ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL AND collection_type = 'Series'
                DO UPDATE SET title = EXCLUDED.title, release_date = EXCLUDED.release_date,
                              media_type_id = EXCLUDED.media_type_id, updated_at = now()
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
                WHERE {after}
                GROUP BY e.parent_tconst, e.season_number, mc.id
                ORDER BY e.parent_tconst, e.season_number
                LIMIT {maxGroups}
            ), upsert AS (
                INSERT INTO media_collections
                    (title, release_date, external_id, external_source, collection_type,
                     media_type_id, parent_media_collection_id, created_at, updated_at)
                SELECT CASE WHEN season_number = -1 THEN 'Unknown' ELSE season_number::text END,
                       CASE WHEN season_start_year IS NULL THEN NULL ELSE make_date(season_start_year, 7, 1) END,
                       parent_tconst, '{nameof(MediaExternalSource.Imdb)}', 'Season', -4, parent_id, now(), now()
                FROM groups
                ON CONFLICT (title, collection_type, media_type_id, parent_media_collection_id)
                    WHERE parent_media_collection_id IS NOT NULL
                DO UPDATE SET release_date = EXCLUDED.release_date, external_id = EXCLUDED.external_id,
                              external_source = EXCLUDED.external_source, updated_at = now()
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
                       season.id AS season_id
                FROM candidates
                INNER JOIN imdb_import_episodes e ON e.tconst = candidates.tconst
                INNER JOIN media_collections series ON series.external_id = e.parent_tconst
                                                    AND series.external_source = '{nameof(MediaExternalSource.Imdb)}'
                                                    AND series.collection_type = 'Series'
                INNER JOIN media_collections season ON season.parent_media_collection_id = series.id
                                                    AND season.collection_type = 'Season'
                                                    AND season.media_type_id = -4
                                                    AND season.title = CASE WHEN e.season_number = -1 THEN 'Unknown' ELSE e.season_number::text END
            ), upsert AS (
                INSERT INTO media (title, release_date, external_id, external_source,
                                   media_type_id, media_collection_id, created_at, updated_at)
                SELECT s.primary_title,
                       CASE WHEN s.start_year IS NULL THEN NULL ELSE make_date(s.start_year, 7, 1) END,
                       s.tconst, '{nameof(MediaExternalSource.Imdb)}', -4, s.season_id, now(), now()
                FROM selected s
                ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL
                DO UPDATE SET title = EXCLUDED.title, release_date = EXCLUDED.release_date,
                              media_type_id = EXCLUDED.media_type_id, media_collection_id = EXCLUDED.media_collection_id,
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
}

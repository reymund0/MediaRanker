using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Data;

public class ImdbLoadSqlProvider(PostgreSQLContext dbContext, ILogger<ImdbLoadSqlProvider> logger) : IImdbLoadProvider
{
    public async Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct)
    {
        // NOTE: With ON CONFLICT DO UPDATE, Postgres reports both inserted and updated rows in the
        // affected-row count. We cannot cheaply distinguish inserted vs updated without RETURNING xmax = 0.
        // Logging a single "affected" count is acceptable for now.
        var sql = $"""
            INSERT INTO media (title, release_date, external_id, external_source, media_type_id, created_at, updated_at)
            SELECT
                i.primary_title,
                CASE WHEN i.start_year IS NULL THEN NULL ELSE make_date(i.start_year, 7, 1) END,
                i.tconst,
                '{nameof(MediaExternalSource.Imdb)}',
                CASE i.title_type
                    WHEN 'videoGame' THEN -1
                    WHEN 'movie'     THEN -3
                    WHEN 'tvMovie'   THEN -3
                    WHEN 'short'     THEN -3
                    WHEN 'tvShort'   THEN -3
                    WHEN 'video'     THEN -3
                END,
                now(),
                now()
            FROM imdb_imports i
            WHERE i.title_type IN ('videoGame', 'movie', 'tvMovie', 'short', 'tvShort', 'video')
            ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL
            DO UPDATE SET
                title          = EXCLUDED.title,
                release_date   = EXCLUDED.release_date,
                media_type_id  = EXCLUDED.media_type_id,
                updated_at     = now();
            """;
        try
        {
            // Set a longer timeout for this operation since we're processing millions of rows.
            dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));

            var affected = await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            return new ImdbLoadResult(affected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading non-series media from imdb_imports. SQL: {Sql}", sql);
            throw;
        }
        finally
        {
            // Reset the command timeout to the default value.
            // Not really necessary since dbContext is disposed after loading, resetting the config.
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    public async Task<ImdbLoadResult> LoadSeriesCollectionsAsync(CancellationToken ct)
    {
        var sql = $"""
            INSERT INTO media_collections
                (title, release_date, external_id, external_source, collection_type,
                 media_type_id, parent_media_collection_id, created_at, updated_at)
            SELECT
                i.primary_title,
                CASE WHEN i.start_year IS NULL THEN NULL ELSE make_date(i.start_year, 7, 1) END,
                i.tconst,
                '{nameof(MediaExternalSource.Imdb)}',
                'Series',
                -4,
                NULL,
                now(),
                now()
            FROM imdb_imports i
            WHERE i.title_type IN ('tvSeries', 'tvMiniSeries')
            ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL AND collection_type = 'Series'
            DO UPDATE SET
                title         = EXCLUDED.title,
                release_date  = EXCLUDED.release_date,
                media_type_id = EXCLUDED.media_type_id,
                updated_at    = now();
            """;
        try
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));

            var affected = await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            return new ImdbLoadResult(affected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading series collections from imdb_imports. SQL: {Sql}", sql);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    public async Task<ImdbLoadResult> LoadSeasonCollectionsAsync(CancellationToken ct)
    {
        // Conflict target matches uq_media_collections_title_type_mediatype_parent:
        // (title, collection_type, media_type_id, parent_media_collection_id) WHERE parent IS NOT NULL.
        var sql = $"""
            INSERT INTO media_collections
                (title, release_date, external_id, external_source, collection_type,
                 media_type_id, parent_media_collection_id, created_at, updated_at)
            SELECT
                CASE WHEN agg.season_number = -1 THEN 'Unknown' ELSE agg.season_number::text END,
                CASE WHEN agg.season_start_year IS NULL THEN NULL ELSE make_date(agg.season_start_year, 7, 1) END,
                agg.parent_tconst,
                '{nameof(MediaExternalSource.Imdb)}',
                'Season',
                -4,
                agg.parent_id,
                now(),
                now()
            FROM (
                SELECT mc.id AS parent_id,
                       e.parent_tconst,
                       e.season_number,
                       MIN(i.start_year) AS season_start_year
                FROM imdb_import_episodes e
                INNER JOIN imdb_imports i       ON i.tconst = e.tconst
                INNER JOIN media_collections mc ON mc.external_id = e.parent_tconst
                                                AND mc.external_source = '{nameof(MediaExternalSource.Imdb)}'
                                                AND mc.collection_type = 'Series'
                GROUP BY mc.id, e.parent_tconst, e.season_number
            ) agg
            ON CONFLICT (title, collection_type, media_type_id, parent_media_collection_id)
                WHERE parent_media_collection_id IS NOT NULL
            DO UPDATE SET
                release_date    = EXCLUDED.release_date,
                external_id     = EXCLUDED.external_id,
                external_source = EXCLUDED.external_source,
                updated_at      = now();
            """;
        try
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));

            var affected = await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            return new ImdbLoadResult(affected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading season collections from imdb_import_episodes. SQL: {Sql}", sql);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

    public async Task<ImdbLoadResult> LoadEpisodeMediaAsync(CancellationToken ct)
    {
        var sql = $"""
            INSERT INTO media (title, release_date, external_id, external_source,
                               media_type_id, media_collection_id, created_at, updated_at)
            SELECT
                i.primary_title,
                CASE WHEN i.start_year IS NULL THEN NULL ELSE make_date(i.start_year, 7, 1) END,
                i.tconst,
                '{nameof(MediaExternalSource.Imdb)}',
                -4,
                season.id,
                now(),
                now()
            FROM imdb_imports i
            INNER JOIN imdb_import_episodes e ON e.tconst = i.tconst
            INNER JOIN media_collections series
                ON series.external_id = e.parent_tconst
               AND series.external_source = '{nameof(MediaExternalSource.Imdb)}'
               AND series.collection_type = 'Series'
            INNER JOIN media_collections season
                ON season.parent_media_collection_id = series.id
               AND season.collection_type = 'Season'
               AND season.title = CASE WHEN e.season_number = -1 THEN 'Unknown'
                                       ELSE e.season_number::text END
            WHERE i.title_type = 'tvEpisode'
            ON CONFLICT (external_id, external_source) WHERE external_id IS NOT NULL
            DO UPDATE SET
                title               = EXCLUDED.title,
                release_date        = EXCLUDED.release_date,
                media_type_id       = EXCLUDED.media_type_id,
                media_collection_id = EXCLUDED.media_collection_id,
                updated_at          = now();
            """;
        try
        {
            dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));

            var affected = await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            return new ImdbLoadResult(affected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading episode media from imdb_imports. SQL: {Sql}", sql);
            throw;
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(null);
        }
    }

}

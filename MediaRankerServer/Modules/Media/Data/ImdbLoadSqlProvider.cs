using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MediaRankerServer.Modules.Media.Data;

public class ImdbLoadSqlProvider(PostgreSQLContext dbContext, ILogger<ImdbLoadSqlProvider> logger) : IImdbLoadProvider
{
    // Maps IMDB title_type values to stable MediaType IDs.
    // -1 = Video Game, -3 = Movie.
    internal static readonly IReadOnlyDictionary<string, long> NonSeriesTitleTypeMap =
        new Dictionary<string, long>
        {
            ["videoGame"] = -1L,
            ["movie"]     = -3L,
            ["tvMovie"]   = -3L,
            ["short"]     = -3L,
            ["tvShort"]   = -3L,
            ["video"]     = -3L,
        };

    public async Task<ImdbLoadResult> LoadNonSeriesMediaAsync(CancellationToken ct)
    {
        var caseClause = BuildCaseClause(NonSeriesTitleTypeMap);
        var inClause   = BuildInClause(NonSeriesTitleTypeMap);
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
                {caseClause},
                now(),
                now()
            FROM imdb_imports i
            WHERE i.title_type IN ({inClause})
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

    internal static string BuildCaseClause(IReadOnlyDictionary<string, long> map)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CASE i.title_type");
        foreach (var (titleType, mediaTypeId) in map)
        {
            sb.AppendLine($"    WHEN '{titleType}' THEN {mediaTypeId}");
        }
        sb.Append("END");
        return sb.ToString();
    }

    internal static string BuildInClause(IReadOnlyDictionary<string, long> map)
    {
        return string.Join(", ", map.Keys.Select(k => $"'{k}'"));
    }
}

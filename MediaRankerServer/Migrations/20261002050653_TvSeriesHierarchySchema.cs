using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations;

public partial class TvSeriesHierarchySchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP VIEW review_details;");

        migrationBuilder.AlterColumn<long>(
            name: "media_id",
            table: "reviews",
            type: "bigint",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint");

        migrationBuilder.AddColumn<long>(
            name: "media_collection_id",
            table: "reviews",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "season_number",
            table: "media_collections",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "episode_number",
            table: "media",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE media_collections
            SET season_number = CASE
                WHEN title ~ '^[0-9]{1,10}$' THEN
                    CASE WHEN title::bigint BETWEEN 1 AND 2147483647 THEN title::integer ELSE NULL END
                ELSE NULL
            END
            WHERE collection_type = 'Season' AND media_type = 'TvShow';

            UPDATE media m
            SET episode_number = ie.episode_number
            FROM imdb_import_episodes ie
            WHERE m.external_source = 'Imdb'
              AND m.media_type = 'TvShow'
              AND m.external_id = ie.tconst
              AND ie.episode_number >= 0;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_reviews_media_collection_id",
            table: "reviews",
            column: "media_collection_id");

        migrationBuilder.CreateIndex(
            name: "uq_reviews_user_media_collection",
            table: "reviews",
            columns: new[] { "user_id", "media_collection_id" },
            unique: true,
            filter: "media_collection_id IS NOT NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_reviews_target",
            table: "reviews",
            sql: "(media_id IS NOT NULL) <> (media_collection_id IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "ix_media_collections_parent_season_number",
            table: "media_collections",
            columns: new[] { "parent_media_collection_id", "season_number" });

        migrationBuilder.CreateIndex(
            name: "ix_media_media_collection_episode_number",
            table: "media",
            columns: new[] { "media_collection_id", "episode_number" });

        migrationBuilder.Sql("""
            CREATE VIEW review_details AS
            SELECT
                r.id,
                r.user_id,
                r.overall_score,
                r.review_title,
                r.notes,
                r.consumed_at,
                r.created_at,
                r.updated_at,
                r.media_id,
                r.media_collection_id,
                r.template_id,
                CASE WHEN r.media_collection_id IS NOT NULL THEN 'Series'
            WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Season' THEN 'Episode'
                     ELSE 'Title' END AS review_kind,
                COALESCE(reviewed_series.title, m.title) AS media_title,
                CASE WHEN r.media_collection_id IS NOT NULL THEN reviewed_series.cover_id
                     WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Season' THEN series.cover_id
                     WHEN m.external_source = 'Imdb' AND m.media_type = 'TvShow'
                         THEN CASE WHEN collection.collection_type = 'Series' THEN collection.cover_id ELSE series.cover_id END
                     ELSE m.cover_id END AS media_cover_id,
                COALESCE(reviewed_series.media_type, m.media_type) AS media_type,
                CASE WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Series' THEN collection.id ELSE COALESCE(reviewed_series.id, series.id) END AS series_id,
                COALESCE(reviewed_series.title, series.title, CASE WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Series' THEN collection.title END) AS series_title,
                collection.season_number,
                m.episode_number,
                EXTRACT(YEAR FROM COALESCE(reviewed_series.release_date, series.release_date))::int AS series_start_year,
                EXTRACT(YEAR FROM COALESCE(latest_season.release_date, reviewed_series.release_date, series.release_date))::int AS series_end_year,
                hierarchy.season_count,
                hierarchy.episode_count,
                t.name AS template_name
            FROM reviews r
            LEFT JOIN media m ON r.media_id = m.id
            LEFT JOIN media_collections collection ON m.media_collection_id = collection.id
            LEFT JOIN media_collections series ON series.media_type = 'TvShow' AND series.collection_type = 'Series'
                AND (collection.parent_media_collection_id = series.id OR collection.id = series.id)
            LEFT JOIN media_collections reviewed_series ON r.media_collection_id = reviewed_series.id
            LEFT JOIN LATERAL (
                SELECT
                    COUNT(*)::int AS season_count,
                    COALESCE(SUM(episode_counts.episode_count), 0)::int AS episode_count
                FROM media_collections numbered_seasons
                LEFT JOIN LATERAL (
                    SELECT COUNT(*)::int AS episode_count
                    FROM media episodes
                    WHERE episodes.media_collection_id = numbered_seasons.id
                      AND episodes.media_type = 'TvShow'
                ) episode_counts ON TRUE
                WHERE numbered_seasons.parent_media_collection_id = COALESCE(
                    reviewed_series.id,
                    series.id,
                    CASE WHEN collection.collection_type = 'Series' THEN collection.id END)
                  AND numbered_seasons.collection_type = 'Season'
                  AND numbered_seasons.media_type = 'TvShow'
                  AND numbered_seasons.season_number IS NOT NULL
            ) hierarchy ON TRUE
            LEFT JOIN LATERAL (
                SELECT numbered_seasons.release_date
                FROM media_collections numbered_seasons
                WHERE numbered_seasons.parent_media_collection_id = COALESCE(
                    reviewed_series.id,
                    series.id,
                    CASE WHEN collection.collection_type = 'Series' THEN collection.id END)
                  AND numbered_seasons.collection_type = 'Season'
                  AND numbered_seasons.media_type = 'TvShow'
                  AND numbered_seasons.season_number IS NOT NULL
                ORDER BY numbered_seasons.season_number DESC
                LIMIT 1
            ) latest_season ON TRUE
            INNER JOIN templates t ON r.template_id = t.id;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM reviews WHERE media_collection_id IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot remove TV series review support while series reviews exist';
                END IF;
            END
            $rollback$;
            """);

        migrationBuilder.Sql("DROP VIEW review_details;");

        migrationBuilder.DropIndex(name: "ix_reviews_media_collection_id", table: "reviews");
        migrationBuilder.DropIndex(name: "uq_reviews_user_media_collection", table: "reviews");
        migrationBuilder.DropCheckConstraint(name: "ck_reviews_target", table: "reviews");
        migrationBuilder.DropIndex(name: "ix_media_collections_parent_season_number", table: "media_collections");
        migrationBuilder.DropIndex(name: "ix_media_media_collection_episode_number", table: "media");
        migrationBuilder.DropColumn(name: "media_collection_id", table: "reviews");
        migrationBuilder.DropColumn(name: "season_number", table: "media_collections");
        migrationBuilder.DropColumn(name: "episode_number", table: "media");

        migrationBuilder.AlterColumn<long>(
            name: "media_id",
            table: "reviews",
            type: "bigint",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.Sql("""
            CREATE VIEW review_details AS
            SELECT
                r.id,
                r.user_id,
                r.overall_score,
                r.review_title,
                r.notes,
                r.consumed_at,
                r.created_at,
                r.updated_at,
                r.media_id,
                r.template_id,
                m.title AS media_title,
                CASE WHEN m.external_source = 'Imdb' AND m.media_type = 'TvShow'
                    THEN CASE WHEN collection.collection_type = 'Series' THEN collection.cover_id ELSE series.cover_id END
                    ELSE m.cover_id END AS media_cover_id,
                m.media_type,
                t.name AS template_name
            FROM reviews r
            INNER JOIN media m ON r.media_id = m.id
            LEFT JOIN media_collections collection ON m.media_collection_id = collection.id
            LEFT JOIN media_collections series ON collection.parent_media_collection_id = series.id AND series.collection_type = 'Series'
            INNER JOIN templates t ON r.template_id = t.id;
            """);
    }
}

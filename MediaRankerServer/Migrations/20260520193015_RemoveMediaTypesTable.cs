using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMediaTypesTable : Migration
    {
        /// <inheritdoc />

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail before changing schema if a stored ID or seed row has no canonical enum value.
            migrationBuilder.Sql(@"
DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1 FROM media_types
        WHERE NOT (
            (id = -1 AND name = 'Video Game') OR
            (id = -2 AND name = 'Book') OR
            (id = -3 AND name = 'Movie') OR
            (id = -4 AND name = 'TV Show') OR
            (id = -5 AND name = 'Album') OR
            (id = -6 AND name = 'Concert')
        )
    ) OR
    EXISTS (SELECT 1 FROM media WHERE media_type_id NOT IN (-1, -2, -3, -4, -5, -6)) OR
    EXISTS (SELECT 1 FROM media_collections WHERE media_type_id NOT IN (-1, -2, -3, -4, -5, -6)) OR
    EXISTS (SELECT 1 FROM templates WHERE media_type_id NOT IN (-1, -2, -3, -4, -5, -6))
    THEN
        RAISE EXCEPTION 'Cannot remove media_types: an unmapped media type exists';
    END IF;
END
$migration$;");

            DropViews(migrationBuilder);

            migrationBuilder.AddColumn<string>(
                name: "media_type", table: "templates", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "media_type", table: "media_collections", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "media_type", table: "media", type: "text", nullable: true);

            migrationBuilder.Sql(@"
UPDATE templates SET media_type = CASE media_type_id
    WHEN -1 THEN 'VideoGame' WHEN -2 THEN 'Book' WHEN -3 THEN 'Movie'
    WHEN -4 THEN 'TvShow' WHEN -5 THEN 'Album' WHEN -6 THEN 'Concert' END;
UPDATE media_collections SET media_type = CASE media_type_id
    WHEN -1 THEN 'VideoGame' WHEN -2 THEN 'Book' WHEN -3 THEN 'Movie'
    WHEN -4 THEN 'TvShow' WHEN -5 THEN 'Album' WHEN -6 THEN 'Concert' END;
UPDATE media SET media_type = CASE media_type_id
    WHEN -1 THEN 'VideoGame' WHEN -2 THEN 'Book' WHEN -3 THEN 'Movie'
    WHEN -4 THEN 'TvShow' WHEN -5 THEN 'Album' WHEN -6 THEN 'Concert' END;
ALTER TABLE templates ALTER COLUMN media_type SET NOT NULL;
ALTER TABLE media_collections ALTER COLUMN media_type SET NOT NULL;
ALTER TABLE media ALTER COLUMN media_type SET NOT NULL;");

            migrationBuilder.DropForeignKey(name: "fk_media_media_types_media_type_id", table: "media");
            migrationBuilder.DropForeignKey(name: "fk_media_collections_media_types_media_type_id", table: "media_collections");
            migrationBuilder.DropTable(name: "media_types");

            migrationBuilder.DropIndex(name: "ix_templates_media_type_id", table: "templates");
            migrationBuilder.DropIndex(name: "ix_media_collections_media_type_id", table: "media_collections");
            migrationBuilder.DropIndex(name: "uq_media_collections_title_type_mediatype_parent", table: "media_collections");
            migrationBuilder.DropIndex(name: "ix_media_media_type_id", table: "media");

            migrationBuilder.DropColumn(name: "media_type_id", table: "templates");
            migrationBuilder.DropColumn(name: "media_type_id", table: "media_collections");
            migrationBuilder.DropColumn(name: "media_type_id", table: "media");

            migrationBuilder.CreateIndex(name: "ix_templates_media_type", table: "templates", column: "media_type");
            migrationBuilder.CreateIndex(name: "ix_media_collections_media_type", table: "media_collections", column: "media_type");
            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type", "parent_media_collection_id" },
                unique: true,
                filter: "parent_media_collection_id IS NOT NULL");
            migrationBuilder.CreateIndex(name: "ix_media_media_type", table: "media", column: "media_type");

            CreateViews(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $migration$
BEGIN
    IF EXISTS (SELECT 1 FROM templates WHERE media_type NOT IN ('VideoGame', 'Book', 'Movie', 'TvShow', 'Album', 'Concert')) OR
       EXISTS (SELECT 1 FROM media_collections WHERE media_type NOT IN ('VideoGame', 'Book', 'Movie', 'TvShow', 'Album', 'Concert')) OR
       EXISTS (SELECT 1 FROM media WHERE media_type NOT IN ('VideoGame', 'Book', 'Movie', 'TvShow', 'Album', 'Concert'))
    THEN
        RAISE EXCEPTION 'Cannot restore media_types: an unmapped media type exists';
    END IF;
END
$migration$;");

            DropViews(migrationBuilder);

            migrationBuilder.AddColumn<long>(name: "media_type_id", table: "templates", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<long>(name: "media_type_id", table: "media_collections", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<long>(name: "media_type_id", table: "media", type: "bigint", nullable: true);

            migrationBuilder.Sql(@"
UPDATE templates SET media_type_id = CASE media_type
    WHEN 'VideoGame' THEN -1 WHEN 'Book' THEN -2 WHEN 'Movie' THEN -3
    WHEN 'TvShow' THEN -4 WHEN 'Album' THEN -5 WHEN 'Concert' THEN -6 END;
UPDATE media_collections SET media_type_id = CASE media_type
    WHEN 'VideoGame' THEN -1 WHEN 'Book' THEN -2 WHEN 'Movie' THEN -3
    WHEN 'TvShow' THEN -4 WHEN 'Album' THEN -5 WHEN 'Concert' THEN -6 END;
UPDATE media SET media_type_id = CASE media_type
    WHEN 'VideoGame' THEN -1 WHEN 'Book' THEN -2 WHEN 'Movie' THEN -3
    WHEN 'TvShow' THEN -4 WHEN 'Album' THEN -5 WHEN 'Concert' THEN -6 END;
ALTER TABLE templates ALTER COLUMN media_type_id SET NOT NULL;
ALTER TABLE media_collections ALTER COLUMN media_type_id SET NOT NULL;
ALTER TABLE media ALTER COLUMN media_type_id SET NOT NULL;");

            migrationBuilder.DropColumn(name: "media_type", table: "templates");
            migrationBuilder.DropColumn(name: "media_type", table: "media_collections");
            migrationBuilder.DropColumn(name: "media_type", table: "media");

            migrationBuilder.CreateTable(
                name: "media_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table => table.PrimaryKey("pk_media_types", x => x.id));

            migrationBuilder.InsertData(
                table: "media_types",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { -6L, "Concert" }, { -5L, "Album" }, { -4L, "TV Show" },
                    { -3L, "Movie" }, { -2L, "Book" }, { -1L, "Video Game" }
                });

            migrationBuilder.CreateIndex(name: "uq_media_types_name", table: "media_types", column: "name", unique: true);
            migrationBuilder.CreateIndex(name: "ix_templates_media_type_id", table: "templates", column: "media_type_id");
            migrationBuilder.CreateIndex(name: "ix_media_collections_media_type_id", table: "media_collections", column: "media_type_id");
            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type_id", "parent_media_collection_id" },
                unique: true,
                filter: "parent_media_collection_id IS NOT NULL");
            migrationBuilder.CreateIndex(name: "ix_media_media_type_id", table: "media", column: "media_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_media_media_types_media_type_id", table: "media", column: "media_type_id",
                principalTable: "media_types", principalColumn: "id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "fk_media_collections_media_types_media_type_id", table: "media_collections", column: "media_type_id",
                principalTable: "media_types", principalColumn: "id", onDelete: ReferentialAction.Cascade);

            RestoreOldViews(migrationBuilder);
        }

        private static void DropViews(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS review_details;");
        }

        private static void CreateViews(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'media_covers' AND column_name = 'file_key'
    ) THEN
        EXECUTE $view$
            CREATE VIEW review_details AS
            SELECT r.id, r.user_id, r.overall_score, r.review_title, r.notes,
                   r.consumed_at, r.created_at, r.updated_at, r.media_id, r.template_id,
                   m.title AS media_title, mc.file_key AS media_cover_file_key,
                   m.media_type, t.name AS template_name
            FROM reviews r
            INNER JOIN media m ON r.media_id = m.id
            LEFT JOIN media_covers mc ON mc.id = m.cover_id
            INNER JOIN templates t ON r.template_id = t.id
        $view$;
    ELSE
        EXECUTE $view$
            CREATE VIEW review_details AS
            SELECT r.id, r.user_id, r.overall_score, r.review_title, r.notes,
                   r.consumed_at, r.created_at, r.updated_at, r.media_id, r.template_id,
                   m.title AS media_title,
                   CASE WHEN m.external_source = 'Imdb' AND m.media_type = 'TvShow'
                        THEN CASE WHEN collection.collection_type = 'Series' THEN collection.cover_id ELSE series.cover_id END
                        ELSE m.cover_id END AS media_cover_id,
                   m.media_type, t.name AS template_name
            FROM reviews r
            INNER JOIN media m ON r.media_id = m.id
            LEFT JOIN media_collections collection ON m.media_collection_id = collection.id
            LEFT JOIN media_collections series ON collection.parent_media_collection_id = series.id
                 AND series.collection_type = 'Series'
            INNER JOIN templates t ON r.template_id = t.id
        $view$;
    END IF;
END
$migration$;");
        }

        private static void RestoreOldViews(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'media_covers' AND column_name = 'file_key'
    ) THEN
        EXECUTE $view$
            CREATE VIEW review_details AS
            SELECT r.id, r.user_id, r.overall_score, r.review_title, r.notes,
                   r.consumed_at, r.created_at, r.updated_at, m.id AS media_id,
                   m.title AS media_title, mc.file_key AS media_cover_file_key,
                   m.media_type_id, mt.name AS media_type_name, r.template_id, t.name AS template_name
            FROM reviews r
            INNER JOIN media m ON m.id = r.media_id
            LEFT JOIN media_covers mc ON mc.id = m.cover_id
            INNER JOIN media_types mt ON mt.id = m.media_type_id
            INNER JOIN templates t ON t.id = r.template_id
        $view$;
    ELSE
        EXECUTE $view$
            CREATE VIEW review_details AS
            SELECT r.id, r.user_id, r.overall_score, r.review_title, r.notes,
                   r.consumed_at, r.created_at, r.updated_at, r.media_id, r.template_id,
                   m.title AS media_title,
                   CASE WHEN m.external_source = 'Imdb' AND mt.name = 'TV Show'
                        THEN CASE WHEN collection.collection_type = 'Series' THEN collection.cover_id ELSE series.cover_id END
                        ELSE m.cover_id END AS media_cover_id,
                   m.media_type_id, mt.name AS media_type_name, t.name AS template_name
            FROM reviews r
            INNER JOIN media m ON r.media_id = m.id
            LEFT JOIN media_collections collection ON m.media_collection_id = collection.id
            LEFT JOIN media_collections series ON collection.parent_media_collection_id = series.id
                 AND series.collection_type = 'Series'
            INNER JOIN media_types mt ON m.media_type_id = mt.id
            INNER JOIN templates t ON r.template_id = t.id
        $view$;
    END IF;
END
$migration$;");
        }
    }
}

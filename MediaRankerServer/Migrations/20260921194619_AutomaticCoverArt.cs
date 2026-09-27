using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AutomaticCoverArt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing uploads cannot be translated into provider identities. No S3 objects are deleted.
            migrationBuilder.Sql("DROP VIEW IF EXISTS review_details; UPDATE media SET cover_id = NULL; UPDATE media_collections SET cover_id = NULL; DELETE FROM media_covers;");
            migrationBuilder.DropColumn(
                name: "file_size_bytes",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "marked_for_cleanup",
                table: "media_covers");

            migrationBuilder.RenameColumn(
                name: "file_upload_id",
                table: "media_covers",
                newName: "version");

            migrationBuilder.RenameColumn(
                name: "file_name",
                table: "media_covers",
                newName: "provider");

            migrationBuilder.RenameColumn(
                name: "file_key",
                table: "media_covers",
                newName: "outcome");

            migrationBuilder.RenameColumn(
                name: "file_content_type",
                table: "media_covers",
                newName: "lookup_kind");

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "media_covers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_at",
                table: "media_covers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "claim_token",
                table: "media_covers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_until",
                table: "media_covers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                table: "media_covers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failure_code",
                table: "media_covers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_path",
                table: "media_covers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lookup_id",
                table: "media_covers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "media_covers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_item_id",
                table: "media_covers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "requested_at",
                table: "media_covers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "igdb_import_state",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bootstrap_completed = table.Column<bool>(type: "boolean", nullable: false),
                    run_is_bootstrap = table.Column<bool>(type: "boolean", nullable: false),
                    run_maximum_id = table.Column<long>(type: "bigint", nullable: true),
                    last_committed_id = table.Column<long>(type: "bigint", nullable: false),
                    run_updated_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    run_updated_before = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bootstrap_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_completed_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claim_token = table.Column<Guid>(type: "uuid", nullable: true),
                    claimed_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_igdb_import_state", x => x.id);
                    table.CheckConstraint("ck_igdb_import_state_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "igdb_imports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    igdb_game_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    first_release_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    game_type_id = table.Column<long>(type: "bigint", nullable: true),
                    game_type_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    version_parent_id = table.Column<long>(type: "bigint", nullable: true),
                    cover_image_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    provider_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_igdb_imports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_covers_next_attempt_at_claimed_until",
                table: "media_covers",
                columns: new[] { "next_attempt_at", "claimed_until" });

            migrationBuilder.CreateIndex(
                name: "ix_media_covers_provider_lookup_kind_lookup_id",
                table: "media_covers",
                columns: new[] { "provider", "lookup_kind", "lookup_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_cover_ready",
                table: "media_covers",
                sql: "outcome <> 'Ready' OR (image_path IS NOT NULL AND checked_at IS NOT NULL AND expires_at IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_igdb_imports_admission",
                table: "igdb_imports",
                columns: new[] { "first_release_date", "game_type_name" });

            migrationBuilder.CreateIndex(
                name: "ix_igdb_imports_provider_updated_at",
                table: "igdb_imports",
                column: "provider_updated_at");

            migrationBuilder.CreateIndex(
                name: "uq_igdb_imports_game_id",
                table: "igdb_imports",
                column: "igdb_game_id",
                unique: true);
            migrationBuilder.Sql(@"CREATE VIEW review_details AS
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
" );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Existing uploads cannot be translated into provider identities. No S3 objects are deleted.
            migrationBuilder.Sql("DROP VIEW IF EXISTS review_details; UPDATE media SET cover_id = NULL; UPDATE media_collections SET cover_id = NULL; DELETE FROM media_covers;");
            migrationBuilder.DropTable(
                name: "igdb_import_state");

            migrationBuilder.DropTable(
                name: "igdb_imports");

            migrationBuilder.DropIndex(
                name: "ix_media_covers_next_attempt_at_claimed_until",
                table: "media_covers");

            migrationBuilder.DropIndex(
                name: "ix_media_covers_provider_lookup_kind_lookup_id",
                table: "media_covers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_cover_ready",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "checked_at",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "claim_token",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "claimed_until",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "failure_code",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "image_path",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "lookup_id",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "provider_item_id",
                table: "media_covers");

            migrationBuilder.DropColumn(
                name: "requested_at",
                table: "media_covers");

            migrationBuilder.RenameColumn(
                name: "version",
                table: "media_covers",
                newName: "file_upload_id");

            migrationBuilder.RenameColumn(
                name: "provider",
                table: "media_covers",
                newName: "file_name");

            migrationBuilder.RenameColumn(
                name: "outcome",
                table: "media_covers",
                newName: "file_key");

            migrationBuilder.RenameColumn(
                name: "lookup_kind",
                table: "media_covers",
                newName: "file_content_type");

            migrationBuilder.AddColumn<long>(
                name: "file_size_bytes",
                table: "media_covers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "marked_for_cleanup",
                table: "media_covers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            migrationBuilder.Sql(@"
DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'media' AND column_name = 'media_type'
    ) THEN
        EXECUTE $view$
CREATE VIEW review_details AS
SELECT r.id, r.user_id, r.overall_score, r.review_title, r.notes, r.consumed_at,
       r.created_at, r.updated_at, r.media_id, r.template_id, m.title AS media_title,
       mc.file_key AS media_cover_file_key, m.media_type, t.name AS template_name
FROM reviews r
INNER JOIN media m ON r.media_id = m.id
LEFT JOIN media_covers mc ON mc.id = m.cover_id
INNER JOIN templates t ON r.template_id = t.id
        $view$;
    ELSE
        EXECUTE $view$
CREATE VIEW review_details AS
SELECT r.id, r.user_id, r.overall_score, r.review_title, r.notes, r.consumed_at,
       r.created_at, r.updated_at, r.media_id, r.template_id, m.title AS media_title,
       mc.file_key AS media_cover_file_key, m.media_type_id, mt.name AS media_type_name,
       t.name AS template_name
FROM reviews r
INNER JOIN media m ON r.media_id = m.id
LEFT JOIN media_covers mc ON mc.id = m.cover_id
INNER JOIN media_types mt ON m.media_type_id = mt.id
INNER JOIN templates t ON r.template_id = t.id
        $view$;
    END IF;
END
$migration$;");
        }
    }
}

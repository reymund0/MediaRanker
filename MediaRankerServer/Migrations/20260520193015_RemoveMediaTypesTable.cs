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
            // Drop views.
            DropViews(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "fk_media_media_types_media_type_id",
                table: "media");

            migrationBuilder.DropForeignKey(
                name: "fk_media_collections_media_types_media_type_id",
                table: "media_collections");

            migrationBuilder.DropTable(
                name: "media_types");

            migrationBuilder.DropIndex(
                name: "ix_templates_media_type_id",
                table: "templates");

            migrationBuilder.DropIndex(
                name: "ix_media_collections_media_type_id",
                table: "media_collections");

            migrationBuilder.DropIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections");

            migrationBuilder.DropIndex(
                name: "ix_media_media_type_id",
                table: "media");

            migrationBuilder.DropColumn(
                name: "media_type_id",
                table: "templates");

            migrationBuilder.DropColumn(
                name: "media_type_id",
                table: "media_collections");

            migrationBuilder.DropColumn(
                name: "media_type_id",
                table: "media");

            migrationBuilder.AddColumn<string>(
                name: "media_type",
                table: "templates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "media_type",
                table: "media_collections",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "media_type",
                table: "media",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "templates",
                keyColumn: "id",
                keyValue: -1L,
                column: "media_type",
                value: "VideoGame");

            migrationBuilder.CreateIndex(
                name: "ix_templates_media_type",
                table: "templates",
                column: "media_type");

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_media_type",
                table: "media_collections",
                column: "media_type");

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type", "parent_media_collection_id" },
                unique: true,
                filter: "parent_media_collection_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_media_media_type",
                table: "media",
                column: "media_type");

            // Recreate Views
            CreateViews(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop dependent views
            DropViews(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "ix_templates_media_type",
                table: "templates");

            migrationBuilder.DropIndex(
                name: "ix_media_collections_media_type",
                table: "media_collections");

            migrationBuilder.DropIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections");

            migrationBuilder.DropIndex(
                name: "ix_media_media_type",
                table: "media");

            migrationBuilder.DropColumn(
                name: "media_type",
                table: "templates");

            migrationBuilder.DropColumn(
                name: "media_type",
                table: "media_collections");

            migrationBuilder.DropColumn(
                name: "media_type",
                table: "media");

            migrationBuilder.AddColumn<long>(
                name: "media_type_id",
                table: "templates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "media_type_id",
                table: "media_collections",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "media_type_id",
                table: "media",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "media_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_types", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "media_types",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { -6L, "Concert" },
                    { -5L, "Album" },
                    { -4L, "TV Show" },
                    { -3L, "Movie" },
                    { -2L, "Book" },
                    { -1L, "Video Game" }
                });

            migrationBuilder.UpdateData(
                table: "templates",
                keyColumn: "id",
                keyValue: -1L,
                column: "media_type_id",
                value: -1L);

            migrationBuilder.CreateIndex(
                name: "ix_templates_media_type_id",
                table: "templates",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_media_type_id",
                table: "media_collections",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type_id", "parent_media_collection_id" },
                unique: true,
                filter: "parent_media_collection_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_media_media_type_id",
                table: "media",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_media_types_name",
                table: "media_types",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_media_media_types_media_type_id",
                table: "media",
                column: "media_type_id",
                principalTable: "media_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_media_collections_media_types_media_type_id",
                table: "media_collections",
                column: "media_type_id",
                principalTable: "media_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Recreate old version of views.
            RestoreOldViews(migrationBuilder);
        }

        private static void DropViews(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS review_details");
        }

        private static void CreateViews(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
                    m.id AS media_id,
                    m.title AS media_title,
                    mc.file_key AS media_cover_file_key,
                    m.media_type,
                    r.template_id,
                    t.name AS template_name
                FROM reviews r
                INNER JOIN media m ON m.id = r.media_id
                INNER JOIN templates t ON t.id = r.template_id
                LEFT JOIN media_covers mc ON mc.id = m.cover_id;
            ");
        }

        private static void RestoreOldViews(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
                    m.id AS media_id,
                    m.title AS media_title,
                    mc.file_key AS media_cover_file_key,
                    mt.name AS media_type_name,
                    r.template_id,
                    t.name AS template_name
                FROM reviews r
                INNER JOIN media m ON m.id = r.media_id
                INNER JOIN media_types mt ON mt.id = m.media_type_id
                INNER JOIN templates t ON t.id = r.template_id
                LEFT JOIN media_covers mc ON mc.id = m.cover_id;
            ");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaCollectionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "media_collection_id",
                table: "media",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_collections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    collection_type = table.Column<string>(type: "text", nullable: false),
                    media_type_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_media_collection_id = table.Column<long>(type: "bigint", nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    cover_file_upload_id = table.Column<long>(type: "bigint", nullable: true),
                    cover_file_key = table.Column<string>(type: "text", nullable: true),
                    cover_file_name = table.Column<string>(type: "text", nullable: true),
                    cover_file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    cover_file_content_type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_collections", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_collections_media_collections_parent_media_collection",
                        column: x => x.parent_media_collection_id,
                        principalTable: "media_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_media_collections_media_types_media_type_id",
                        column: x => x.media_type_id,
                        principalTable: "media_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_media_collection_id",
                table: "media",
                column: "media_collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_media_type_id",
                table: "media_collections",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_parent_id",
                table: "media_collections",
                column: "parent_media_collection_id");

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type_id", "parent_media_collection_id" },
                unique: true,
                filter: "parent_media_collection_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_root",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type_id" },
                unique: true,
                filter: "parent_media_collection_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_media_media_collections_media_collection_id",
                table: "media",
                column: "media_collection_id",
                principalTable: "media_collections",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_media_media_collections_media_collection_id",
                table: "media");

            migrationBuilder.DropTable(
                name: "media_collections");

            migrationBuilder.DropIndex(
                name: "ix_media_media_collection_id",
                table: "media");

            migrationBuilder.DropColumn(
                name: "media_collection_id",
                table: "media");
        }
    }
}

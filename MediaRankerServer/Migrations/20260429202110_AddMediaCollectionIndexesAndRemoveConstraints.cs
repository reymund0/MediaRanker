using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaCollectionIndexesAndRemoveConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_media_collections_title_type_mediatype_root",
                table: "media_collections");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "release_date",
                table: "media_collections",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_external_id",
                table: "media_collections",
                column: "external_id");

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_external_id_source_series",
                table: "media_collections",
                columns: new[] { "external_id", "external_source" },
                unique: true,
                filter: "external_id IS NOT NULL AND collection_type = 'Series'");

            migrationBuilder.CreateIndex(
                name: "ix_media_external_id",
                table: "media",
                column: "external_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_media_collections_external_id",
                table: "media_collections");

            migrationBuilder.DropIndex(
                name: "uq_media_collections_external_id_source_series",
                table: "media_collections");

            migrationBuilder.DropIndex(
                name: "ix_media_external_id",
                table: "media");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "release_date",
                table: "media_collections",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_root",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type_id" },
                unique: true,
                filter: "parent_media_collection_id IS NULL");
        }
    }
}

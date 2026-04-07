using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbImportAndMediaExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_source",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "imdb_imports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "text", nullable: false),
                    title_type = table.Column<string>(type: "text", nullable: false),
                    primary_title = table.Column<string>(type: "text", nullable: false),
                    original_title = table.Column<string>(type: "text", nullable: false),
                    is_adult = table.Column<bool>(type: "boolean", nullable: false),
                    start_year = table.Column<int>(type: "integer", nullable: true),
                    end_year = table.Column<int>(type: "integer", nullable: true),
                    runtime_minutes = table.Column<int>(type: "integer", nullable: true),
                    genres = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_imports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_media_external_id_source",
                table: "media",
                columns: new[] { "external_id", "external_source" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_imdb_imports_title_type",
                table: "imdb_imports",
                column: "title_type");

            migrationBuilder.CreateIndex(
                name: "uq_imdb_imports_tconst",
                table: "imdb_imports",
                column: "tconst",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imdb_imports");

            migrationBuilder.DropIndex(
                name: "uq_media_external_id_source",
                table: "media");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "media");

            migrationBuilder.DropColumn(
                name: "external_source",
                table: "media");
        }
    }
}

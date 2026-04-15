using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbImportEpisodesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "imdb_import_episodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "text", nullable: false),
                    parent_tconst = table.Column<string>(type: "text", nullable: false),
                    season_number = table.Column<int>(type: "integer", nullable: false),
                    episode_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_import_episodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_imdb_import_episodes_parent_tconst",
                table: "imdb_import_episodes",
                column: "parent_tconst");

            migrationBuilder.CreateIndex(
                name: "uq_imdb_import_episodes_tconst",
                table: "imdb_import_episodes",
                column: "tconst",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imdb_import_episodes");
        }
    }
}

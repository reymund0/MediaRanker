using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRawLineToImdbImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Also clear out the existing records to avoid null values
            migrationBuilder.Sql("DELETE FROM imdb_imports");
            migrationBuilder.Sql("DELETE FROM imdb_import_episodes");
            
            migrationBuilder.AddColumn<string>(
                name: "raw_line",
                table: "imdb_imports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "raw_line",
                table: "imdb_import_episodes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "raw_line",
                table: "imdb_imports");

            migrationBuilder.DropColumn(
                name: "raw_line",
                table: "imdb_import_episodes");
        }
    }
}

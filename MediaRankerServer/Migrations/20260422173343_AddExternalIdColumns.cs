using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "media_collections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_source",
                table: "media_collections",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "external_id",
                table: "media_collections");

            migrationBuilder.DropColumn(
                name: "external_source",
                table: "media_collections");
        }
    }
}

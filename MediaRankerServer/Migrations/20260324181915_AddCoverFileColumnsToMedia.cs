using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverFileColumnsToMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_file_content_type",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_file_key",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_file_name",
                table: "media",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cover_file_size_bytes",
                table: "media",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cover_file_upload_id",
                table: "media",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_file_content_type",
                table: "media");

            migrationBuilder.DropColumn(
                name: "cover_file_key",
                table: "media");

            migrationBuilder.DropColumn(
                name: "cover_file_name",
                table: "media");

            migrationBuilder.DropColumn(
                name: "cover_file_size_bytes",
                table: "media");

            migrationBuilder.DropColumn(
                name: "cover_file_upload_id",
                table: "media");
        }
    }
}

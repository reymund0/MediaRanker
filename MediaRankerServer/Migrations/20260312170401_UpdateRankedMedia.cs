using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRankedMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_ranked_media_user_media_template",
                table: "ranked_media");

            migrationBuilder.CreateIndex(
                name: "uq_ranked_media_user_media",
                table: "ranked_media",
                columns: new[] { "user_id", "media_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_ranked_media_user_media",
                table: "ranked_media");

            migrationBuilder.CreateIndex(
                name: "uq_ranked_media_user_media_template",
                table: "ranked_media",
                columns: new[] { "user_id", "media_id", "template_id" },
                unique: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class DropTemplateFieldDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_templates_system_name",
                table: "templates");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "template_fields");

            migrationBuilder.CreateIndex(
                name: "uq_templates_is_system",
                table: "templates",
                column: "name",
                filter: "id < 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_templates_is_system",
                table: "templates");

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "template_fields",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "uq_templates_system_name",
                table: "templates",
                column: "name",
                filter: "user_id = 'system'");
        }
    }
}

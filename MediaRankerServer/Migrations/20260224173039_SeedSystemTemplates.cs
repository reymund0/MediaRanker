using Microsoft.EntityFrameworkCore.Migrations;

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class SeedSystemTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO templates (id, user_id, name, description)
                VALUES (-1, 'system', 'Video Games', 'Default review template for video games.');

                INSERT INTO template_fields (id, template_id, name, display_name, position)
                VALUES 
                    (-11, -1, 'Gameplay', 'Gameplay', 1),
                    (-14, -1, 'Graphics', 'Graphics', 2),
                    (-12, -1, 'Story', 'Story', 3),
                    (-13, -1, 'Sound', 'Sound', 4);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "templates",
                keyColumn: "user_id",
                keyValue: "system"
            );
        }
    }
}

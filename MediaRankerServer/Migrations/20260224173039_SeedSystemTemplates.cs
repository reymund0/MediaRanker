using System.Linq;
using MediaRankerServer.Data.Seeds;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class SeedSystemTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var seeds = SystemTemplates.GenerateSeeds().ToArray();

            foreach (var (template, fields) in seeds)
            {
                migrationBuilder.InsertData(
                    table: "templates",
                    columns: ["id", "user_id", "name", "description"],
                    values: [template.Id, template.UserId, template.Name, template.Description]
                );

                foreach (var field in fields)
                {
                    migrationBuilder.InsertData(
                        table: "template_fields",
                        columns: ["id", "template_id", "name", "display_name", "position"],
                        values:
                        [
                            field.Id,
                            field.TemplateId,
                            field.Name,
                            field.DisplayName,
                            field.Position
                        ]
                    );
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "templates",
                keyColumn: "user_id",
                keyValue: SeedUtils.SystemUserId
            );
        }
    }
}

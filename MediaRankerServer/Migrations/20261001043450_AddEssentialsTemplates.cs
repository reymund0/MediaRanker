using MediaRankerServer.Modules.Templates.Data.Seeds;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations;

public partial class AddEssentialsTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var template in EssentialsTemplateSeeds.Templates)
            migrationBuilder.InsertData("templates",
                new[] { "id", "description", "media_type", "name", "user_id" },
                new object[] { template.Id, template.Description!, template.MediaType, template.Name, template.UserId });

        foreach (var field in EssentialsTemplateSeeds.Fields)
            migrationBuilder.InsertData("template_fields",
                new[] { "id", "name", "position", "template_id" },
                new object[] { field.Id, field.Name, field.Position, field.TemplateId });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        var ids = string.Join(",", EssentialsTemplateSeeds.Templates.Select(template => template.Id));
        // Cross-module references have no FK: refuse to orphan reviews during rollback.
        migrationBuilder.Sql($"""
            DO $rollback$
            BEGIN
                IF EXISTS (SELECT 1 FROM reviews WHERE template_id IN ({ids})) THEN
                    RAISE EXCEPTION 'Cannot remove starter templates while reviews use them';
                END IF;
            END
            $rollback$;
            """);
        foreach (var field in EssentialsTemplateSeeds.Fields)
            migrationBuilder.DeleteData("template_fields", "id", field.Id);
        foreach (var template in EssentialsTemplateSeeds.Templates)
            migrationBuilder.DeleteData("templates", "id", template.Id);
    }
}

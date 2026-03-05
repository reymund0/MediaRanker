using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            -- INSERT system media types
            INSERT INTO media_types (id, name)
            VALUES 
                (-1, 'Video Game'),
                (-2, 'Book'),
                (-3, 'Movie'),
                (-4, 'TV Show'),
                (-5, 'Album'),
                (-6, 'Concert');
            ");

            migrationBuilder.Sql(@"
            -- Seed System Templates
            INSERT INTO templates (id, media_type_id, user_id, name, description)
            VALUES (-1, -1, 'system', 'Video Games', 'Default review template for video games.');

            -- Seed System Template Fields
            INSERT INTO template_fields (id, template_id, name, position)
            VALUES 
                (-11, -1, 'Gameplay', 0),
                (-12, -1, 'Graphics', 1),
                (-13, -1, 'Story', 2),
                (-14, -1, 'Sound', 3);
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            -- Delete system template fields
            DELETE FROM template_fields WHERE id < 0;
            
            -- Delete system template
            DELETE FROM templates WHERE id < 0;
            
            -- Delete system media types
            DELETE FROM media_types WHERE id < 0;
            ");
        }
    }
}

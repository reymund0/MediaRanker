using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class CreateViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE VIEW review_details AS
                SELECT
                    r.id,
                    r.user_id,
                    r.overall_score,
                    r.review_title,
                    r.notes,
                    r.consumed_at,
                    r.created_at,
                    r.updated_at,
                    r.media_id,
                    r.template_id,
                    m.title AS media_title,
                    mc.file_key AS media_cover_file_key,
                    m.media_type_id,
                    mt.name AS media_type_name,
                    t.name AS template_name
                FROM reviews r
                INNER JOIN media m ON r.media_id = m.id
                LEFT JOIN media_covers mc ON m.cover_id = mc.id
                INNER JOIN media_types mt ON m.media_type_id = mt.id
                INNER JOIN templates t ON r.template_id = t.id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS review_details;");
        }
    }
}

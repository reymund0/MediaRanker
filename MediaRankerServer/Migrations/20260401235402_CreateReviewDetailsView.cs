using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class CreateReviewDetailsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_review_fields_template_fields_template_field_id",
                table: "review_fields");

            migrationBuilder.DropForeignKey(
                name: "fk_reviews_media_media_id",
                table: "reviews");

            migrationBuilder.DropForeignKey(
                name: "fk_reviews_templates_template_id",
                table: "reviews");

            migrationBuilder.DropForeignKey(
                name: "fk_templates_media_types_media_type_id",
                table: "templates");

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
                m.cover_file_key AS media_cover_file_key,
                m.media_type_id,
                mt.name AS media_type_name,
                t.name AS template_name
            FROM reviews r
            INNER JOIN media m ON r.media_id = m.id
            INNER JOIN media_types mt ON m.media_type_id = mt.id
            INNER JOIN templates t ON r.template_id = t.id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS review_details;");

            migrationBuilder.AddForeignKey(
                name: "fk_review_fields_template_fields_template_field_id",
                table: "review_fields",
                column: "template_field_id",
                principalTable: "template_fields",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reviews_media_media_id",
                table: "reviews",
                column: "media_id",
                principalTable: "media",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_reviews_templates_template_id",
                table: "reviews",
                column: "template_id",
                principalTable: "templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_templates_media_types_media_type_id",
                table: "templates",
                column: "media_type_id",
                principalTable: "media_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

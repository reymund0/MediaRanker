using System;
using MediaRankerServer.Data.Entities;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:media_type", "album,book,game,movie,other,tv");

            migrationBuilder.CreateTable(
                name: "media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    media_type = table.Column<MediaType>(type: "media_type", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ranked_media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    media_id = table.Column<long>(type: "bigint", nullable: false),
                    template_id = table.Column<long>(type: "bigint", nullable: false),
                    overall_score = table.Column<short>(type: "smallint", nullable: false),
                    review_title = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ranked_media", x => x.id);
                    table.CheckConstraint("ck_ranked_media_overall_score", "overall_score BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_ranked_media_media_media_id",
                        column: x => x.media_id,
                        principalTable: "media",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ranked_media_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "template_fields",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_template_fields_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ranked_media_scores",
                columns: table => new
                {
                    ranked_media_id = table.Column<long>(type: "bigint", nullable: false),
                    template_field_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ranked_media_scores", x => new { x.ranked_media_id, x.template_field_id });
                    table.CheckConstraint("ck_ranked_media_scores_value", "value BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_ranked_media_scores_ranked_media_ranked_media_id",
                        column: x => x.ranked_media_id,
                        principalTable: "ranked_media",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ranked_media_scores_template_fields_template_field_id",
                        column: x => x.template_field_id,
                        principalTable: "template_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_media_type",
                table: "media",
                column: "media_type");

            migrationBuilder.CreateIndex(
                name: "ix_media_release_date",
                table: "media",
                column: "release_date");

            migrationBuilder.CreateIndex(
                name: "uq_media_title_type_release_date",
                table: "media",
                columns: new[] { "title", "media_type", "release_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ranked_media_media_id",
                table: "ranked_media",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_ranked_media_template_id",
                table: "ranked_media",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_ranked_media_user",
                table: "ranked_media",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ranked_media_user_template",
                table: "ranked_media",
                columns: new[] { "user_id", "template_id" });

            migrationBuilder.CreateIndex(
                name: "uq_ranked_media_user_media_template",
                table: "ranked_media",
                columns: new[] { "user_id", "media_id", "template_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ranked_media_scores_field",
                table: "ranked_media_scores",
                column: "template_field_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_fields_template_id",
                table: "template_fields",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "uq_template_fields_template_name",
                table: "template_fields",
                columns: new[] { "template_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_template_fields_template_position",
                table: "template_fields",
                columns: new[] { "template_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_templates_user_id",
                table: "templates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_templates_system_name",
                table: "templates",
                column: "name",
                filter: "user_id = 'system'");

            migrationBuilder.CreateIndex(
                name: "uq_templates_user_name",
                table: "templates",
                columns: new[] { "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ranked_media_scores");

            migrationBuilder.DropTable(
                name: "ranked_media");

            migrationBuilder.DropTable(
                name: "template_fields");

            migrationBuilder.DropTable(
                name: "media");

            migrationBuilder.DropTable(
                name: "templates");
        }
    }
}

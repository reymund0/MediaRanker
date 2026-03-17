using System;
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
            migrationBuilder.CreateTable(
                name: "media_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    media_type_id = table.Column<long>(type: "bigint", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_media_types_media_type_id",
                        column: x => x.media_type_id,
                        principalTable: "media_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    media_type_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_templates_media_types_media_type_id",
                        column: x => x.media_type_id,
                        principalTable: "media_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
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
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.CheckConstraint("ck_reviews_overall_score", "overall_score BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_reviews_media_media_id",
                        column: x => x.media_id,
                        principalTable: "media",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviews_templates_template_id",
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
                name: "review_fields",
                columns: table => new
                {
                    review_id = table.Column<long>(type: "bigint", nullable: false),
                    template_field_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_fields", x => new { x.review_id, x.template_field_id });
                    table.CheckConstraint("ck_review_fields_value", "value BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_review_fields_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_fields_template_fields_template_field_id",
                        column: x => x.template_field_id,
                        principalTable: "template_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_media_type_id",
                table: "media",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_release_date",
                table: "media",
                column: "release_date");

            migrationBuilder.CreateIndex(
                name: "uq_media_title_type_release_date",
                table: "media",
                columns: new[] { "title", "media_type_id", "release_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_media_types_name",
                table: "media_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_fields_field",
                table: "review_fields",
                column: "template_field_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_media_id",
                table: "reviews",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_template_id",
                table: "reviews",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user",
                table: "reviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_user_template",
                table: "reviews",
                columns: new[] { "user_id", "template_id" });

            migrationBuilder.CreateIndex(
                name: "uq_reviews_user_media",
                table: "reviews",
                columns: new[] { "user_id", "media_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_template_fields_template_id",
                table: "template_fields",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_templates_media_type_id",
                table: "templates",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_templates_user_id",
                table: "templates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_templates_is_system",
                table: "templates",
                column: "id",
                filter: "id < 0");

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
                name: "review_fields");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "template_fields");

            migrationBuilder.DropTable(
                name: "media");

            migrationBuilder.DropTable(
                name: "templates");

            migrationBuilder.DropTable(
                name: "media_types");
        }
    }
}

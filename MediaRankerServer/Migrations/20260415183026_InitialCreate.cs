using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_uploads",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    file_key = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    expected_content_type = table.Column<string>(type: "text", nullable: false),
                    expected_file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    actual_content_type = table.Column<string>(type: "text", nullable: true),
                    actual_file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    state = table.Column<string>(type: "text", nullable: false, defaultValue: "Uploading")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_uploads", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "imdb_import_episodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "text", nullable: false),
                    parent_tconst = table.Column<string>(type: "text", nullable: false),
                    season_number = table.Column<int>(type: "integer", nullable: false),
                    episode_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_import_episodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "imdb_imports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tconst = table.Column<string>(type: "text", nullable: false),
                    title_type = table.Column<string>(type: "text", nullable: false),
                    primary_title = table.Column<string>(type: "text", nullable: false),
                    original_title = table.Column<string>(type: "text", nullable: false),
                    is_adult = table.Column<bool>(type: "boolean", nullable: false),
                    start_year = table.Column<int>(type: "integer", nullable: true),
                    end_year = table.Column<int>(type: "integer", nullable: true),
                    runtime_minutes = table.Column<int>(type: "integer", nullable: true),
                    genres = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imdb_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_covers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_upload_id = table.Column<long>(type: "bigint", nullable: false),
                    file_key = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_content_type = table.Column<string>(type: "text", nullable: false),
                    marked_for_cleanup = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_covers", x => x.id);
                });

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
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    overall_score = table.Column<short>(type: "smallint", nullable: false),
                    review_title = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    media_id = table.Column<long>(type: "bigint", nullable: false),
                    template_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.CheckConstraint("ck_reviews_overall_score", "overall_score BETWEEN 1 AND 10");
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
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    media_type_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_collections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    collection_type = table.Column<string>(type: "text", nullable: false),
                    parent_media_collection_id = table.Column<long>(type: "bigint", nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    cover_id = table.Column<long>(type: "bigint", nullable: true),
                    media_type_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_collections", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_collections_media_collections_parent_media_collection",
                        column: x => x.parent_media_collection_id,
                        principalTable: "media_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_media_collections_media_covers_cover_id",
                        column: x => x.cover_id,
                        principalTable: "media_covers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_media_collections_media_types_media_type_id",
                        column: x => x.media_type_id,
                        principalTable: "media_types",
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
                });

            migrationBuilder.CreateTable(
                name: "template_fields",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
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
                name: "media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    external_source = table.Column<string>(type: "text", nullable: true),
                    media_type_id = table.Column<long>(type: "bigint", nullable: false),
                    media_collection_id = table.Column<long>(type: "bigint", nullable: true),
                    cover_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_media_collections_media_collection_id",
                        column: x => x.media_collection_id,
                        principalTable: "media_collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_media_media_covers_cover_id",
                        column: x => x.cover_id,
                        principalTable: "media_covers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_media_media_types_media_type_id",
                        column: x => x.media_type_id,
                        principalTable: "media_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "media_types",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { -6L, "Concert" },
                    { -5L, "Album" },
                    { -4L, "TV Show" },
                    { -3L, "Movie" },
                    { -2L, "Book" },
                    { -1L, "Video Game" }
                });

            migrationBuilder.InsertData(
                table: "templates",
                columns: new[] { "id", "description", "media_type_id", "name", "user_id" },
                values: new object[] { -1L, "Default review template for video games.", -1L, "Video Games", "system" });

            migrationBuilder.InsertData(
                table: "template_fields",
                columns: new[] { "id", "name", "position", "template_id" },
                values: new object[,]
                {
                    { -14L, "Sound", 3, -1L },
                    { -13L, "Story", 2, -1L },
                    { -12L, "Graphics", 1, -1L },
                    { -11L, "Gameplay", 0, -1L }
                });

            migrationBuilder.CreateIndex(
                name: "uq_file_uploads_entity_type_file_key",
                table: "file_uploads",
                columns: new[] { "entity_type", "file_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_imdb_import_episodes_parent_tconst",
                table: "imdb_import_episodes",
                column: "parent_tconst");

            migrationBuilder.CreateIndex(
                name: "uq_imdb_import_episodes_tconst",
                table: "imdb_import_episodes",
                column: "tconst",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_imdb_imports_title_type",
                table: "imdb_imports",
                column: "title_type");

            migrationBuilder.CreateIndex(
                name: "uq_imdb_imports_tconst",
                table: "imdb_imports",
                column: "tconst",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_cover_id",
                table: "media",
                column: "cover_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_media_collection_id",
                table: "media",
                column: "media_collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_media_type_id",
                table: "media",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_release_date",
                table: "media",
                column: "release_date");

            migrationBuilder.CreateIndex(
                name: "uq_media_external_id_source",
                table: "media",
                columns: new[] { "external_id", "external_source" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_media_title_type_release_date",
                table: "media",
                columns: new[] { "title", "media_type_id", "release_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_cover_id",
                table: "media_collections",
                column: "cover_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_media_type_id",
                table: "media_collections",
                column: "media_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_collections_parent_id",
                table: "media_collections",
                column: "parent_media_collection_id");

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_parent",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type_id", "parent_media_collection_id" },
                unique: true,
                filter: "parent_media_collection_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_media_collections_title_type_mediatype_root",
                table: "media_collections",
                columns: new[] { "title", "collection_type", "media_type_id" },
                unique: true,
                filter: "parent_media_collection_id IS NULL");

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
                name: "file_uploads");

            migrationBuilder.DropTable(
                name: "imdb_import_episodes");

            migrationBuilder.DropTable(
                name: "imdb_imports");

            migrationBuilder.DropTable(
                name: "media");

            migrationBuilder.DropTable(
                name: "review_fields");

            migrationBuilder.DropTable(
                name: "template_fields");

            migrationBuilder.DropTable(
                name: "media_collections");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "templates");

            migrationBuilder.DropTable(
                name: "media_covers");

            migrationBuilder.DropTable(
                name: "media_types");
        }
    }
}

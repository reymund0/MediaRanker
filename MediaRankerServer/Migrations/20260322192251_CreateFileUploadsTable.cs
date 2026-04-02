using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class CreateFileUploadsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "template_fields",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "template_fields",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

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

            migrationBuilder.CreateIndex(
                name: "uq_file_uploads_entity_type_file_key",
                table: "file_uploads",
                columns: new[] { "entity_type", "file_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_uploads");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "template_fields");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "template_fields");
        }
    }
}

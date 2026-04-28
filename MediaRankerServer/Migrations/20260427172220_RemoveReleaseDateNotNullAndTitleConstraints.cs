using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReleaseDateNotNullAndTitleConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_media_title_type_release_date",
                table: "media");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "release_date",
                table: "media",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "release_date",
                table: "media",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_media_title_type_release_date",
                table: "media",
                columns: new[] { "title", "media_type_id", "release_date" },
                unique: true);
        }
    }
}

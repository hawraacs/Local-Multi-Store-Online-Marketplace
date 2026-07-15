using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryVideoSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Stories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "Stories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "Stories",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Image");

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Stories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "Stories");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Stories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixChatMessageStoryCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Stories_StoryID",
                table: "ChatMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Stories_StoryID",
                table: "ChatMessages",
                column: "StoryID",
                principalTable: "Stories",
                principalColumn: "StoryID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Stories_StoryID",
                table: "ChatMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Stories_StoryID",
                table: "ChatMessages",
                column: "StoryID",
                principalTable: "Stories",
                principalColumn: "StoryID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

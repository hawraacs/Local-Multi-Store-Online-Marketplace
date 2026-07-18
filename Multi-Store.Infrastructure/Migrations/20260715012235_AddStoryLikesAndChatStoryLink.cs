using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryLikesAndChatStoryLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoryID",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoryLikes",
                columns: table => new
                {
                    StoryLikeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryID = table.Column<int>(type: "int", nullable: false),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryLikes", x => x.StoryLikeID);
                    table.ForeignKey(
                        name: "FK_StoryLikes_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryLikes_Stories_StoryID",
                        column: x => x.StoryID,
                        principalTable: "Stories",
                        principalColumn: "StoryID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_StoryID",
                table: "ChatMessages",
                column: "StoryID");

            migrationBuilder.CreateIndex(
                name: "IX_StoryLikes_CustomerID",
                table: "StoryLikes",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_StoryLikes_StoryID_CustomerID",
                table: "StoryLikes",
                columns: new[] { "StoryID", "CustomerID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Stories_StoryID",
                table: "ChatMessages",
                column: "StoryID",
                principalTable: "Stories",
                principalColumn: "StoryID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Stories_StoryID",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "StoryLikes");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_StoryID",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "StoryID",
                table: "ChatMessages");
        }
    }
}

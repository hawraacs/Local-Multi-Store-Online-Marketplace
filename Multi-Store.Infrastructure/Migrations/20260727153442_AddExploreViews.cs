using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExploreViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExploreViews",
                columns: table => new
                {
                    ExploreViewID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExplorePostID = table.Column<int>(type: "int", nullable: false),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExploreViews", x => x.ExploreViewID);
                    table.ForeignKey(
                        name: "FK_ExploreViews_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExploreViews_ExplorePosts_ExplorePostID",
                        column: x => x.ExplorePostID,
                        principalTable: "ExplorePosts",
                        principalColumn: "ExplorePostID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExploreViews_CustomerID",
                table: "ExploreViews",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_ExploreViews_ExplorePostID_CustomerID",
                table: "ExploreViews",
                columns: new[] { "ExplorePostID", "CustomerID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExploreViews");
        }
    }
}

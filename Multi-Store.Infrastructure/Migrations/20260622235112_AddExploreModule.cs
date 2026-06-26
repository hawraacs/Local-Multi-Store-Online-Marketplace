using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExploreModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExplorePosts",
                columns: table => new
                {
                    ExplorePostID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    PostType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(2200)", maxLength: 2200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExplorePosts", x => x.ExplorePostID);
                    table.ForeignKey(
                        name: "FK_ExplorePosts_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExplorePosts_Stores_StoreID",
                        column: x => x.StoreID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExploreComments",
                columns: table => new
                {
                    ExploreCommentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExplorePostID = table.Column<int>(type: "int", nullable: false),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    CommentText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExploreComments", x => x.ExploreCommentID);
                    table.ForeignKey(
                        name: "FK_ExploreComments_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExploreComments_ExplorePosts_ExplorePostID",
                        column: x => x.ExplorePostID,
                        principalTable: "ExplorePosts",
                        principalColumn: "ExplorePostID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExploreLikes",
                columns: table => new
                {
                    ExploreLikeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExplorePostID = table.Column<int>(type: "int", nullable: false),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExploreLikes", x => x.ExploreLikeID);
                    table.ForeignKey(
                        name: "FK_ExploreLikes_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExploreLikes_ExplorePosts_ExplorePostID",
                        column: x => x.ExplorePostID,
                        principalTable: "ExplorePosts",
                        principalColumn: "ExplorePostID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExploreMedia",
                columns: table => new
                {
                    ExploreMediaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExplorePostID = table.Column<int>(type: "int", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MediaUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExploreMedia", x => x.ExploreMediaID);
                    table.ForeignKey(
                        name: "FK_ExploreMedia_ExplorePosts_ExplorePostID",
                        column: x => x.ExplorePostID,
                        principalTable: "ExplorePosts",
                        principalColumn: "ExplorePostID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExploreComments_CustomerID",
                table: "ExploreComments",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_ExploreComments_ExplorePostID_CreatedAt",
                table: "ExploreComments",
                columns: new[] { "ExplorePostID", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExploreLikes_CustomerID",
                table: "ExploreLikes",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_ExploreLikes_ExplorePostID_CustomerID",
                table: "ExploreLikes",
                columns: new[] { "ExplorePostID", "CustomerID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExploreMedia_ExplorePostID_DisplayOrder",
                table: "ExploreMedia",
                columns: new[] { "ExplorePostID", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ExplorePosts_IsActive_CreatedAt",
                table: "ExplorePosts",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExplorePosts_ProductID",
                table: "ExplorePosts",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_ExplorePosts_StoreID_CreatedAt",
                table: "ExplorePosts",
                columns: new[] { "StoreID", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExploreComments");

            migrationBuilder.DropTable(
                name: "ExploreLikes");

            migrationBuilder.DropTable(
                name: "ExploreMedia");

            migrationBuilder.DropTable(
                name: "ExplorePosts");
        }
    }
}

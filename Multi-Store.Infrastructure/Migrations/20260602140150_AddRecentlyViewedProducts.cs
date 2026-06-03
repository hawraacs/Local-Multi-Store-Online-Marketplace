using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecentlyViewedProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "ProductImages");

            migrationBuilder.CreateTable(
                name: "RecentlyViewedProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecentlyViewedProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecentlyViewedProducts_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecentlyViewedProducts_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedProducts_CustomerID",
                table: "RecentlyViewedProducts",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedProducts_ProductID",
                table: "RecentlyViewedProducts",
                column: "ProductID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecentlyViewedProducts");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

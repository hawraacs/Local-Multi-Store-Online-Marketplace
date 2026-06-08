using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreFollow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecentlyViewedProducts_Customers_CustomerID",
                table: "RecentlyViewedProducts");

            migrationBuilder.DropIndex(
                name: "IX_RecentlyViewedProducts_CustomerID",
                table: "RecentlyViewedProducts");

            migrationBuilder.RenameColumn(
                name: "CustomerID",
                table: "RecentlyViewedProducts",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "RecentlyViewedProducts",
                newName: "ID");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ApprovedAt",
                table: "DeliveryPersons",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateTable(
                name: "StoreFollows",
                columns: table => new
                {
                    StoreFollowID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    StoreID = table.Column<int>(type: "int", nullable: false),
                    FollowedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreFollows", x => x.StoreFollowID);
                    table.ForeignKey(
                        name: "FK_StoreFollows_Customers_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreFollows_Stores_StoreID",
                        column: x => x.StoreID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedProducts_CustomerId_ProductID",
                table: "RecentlyViewedProducts",
                columns: new[] { "CustomerId", "ProductID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreFollows_CustomerID",
                table: "StoreFollows",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_StoreFollows_StoreID",
                table: "StoreFollows",
                column: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_RecentlyViewedProducts_Customers_CustomerId",
                table: "RecentlyViewedProducts",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecentlyViewedProducts_Customers_CustomerId",
                table: "RecentlyViewedProducts");

            migrationBuilder.DropTable(
                name: "StoreFollows");

            migrationBuilder.DropIndex(
                name: "IX_RecentlyViewedProducts_CustomerId_ProductID",
                table: "RecentlyViewedProducts");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "RecentlyViewedProducts",
                newName: "CustomerID");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "RecentlyViewedProducts",
                newName: "Id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ApprovedAt",
                table: "DeliveryPersons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedProducts_CustomerID",
                table: "RecentlyViewedProducts",
                column: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_RecentlyViewedProducts_Customers_CustomerID",
                table: "RecentlyViewedProducts",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

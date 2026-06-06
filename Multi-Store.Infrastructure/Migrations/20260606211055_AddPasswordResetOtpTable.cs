using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetOtpTable : Migration
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
                name: "PasswordResetOtps",
                columns: table => new
                {
                    PasswordResetOtpID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    DeliveryMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OtpHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetOtps", x => x.PasswordResetOtpID);
                    table.ForeignKey(
                        name: "FK_PasswordResetOtps_AspNetUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecentlyViewedProducts_CustomerId_ProductID",
                table: "RecentlyViewedProducts",
                columns: new[] { "CustomerId", "ProductID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_UserID",
                table: "PasswordResetOtps",
                column: "UserID");

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
                name: "PasswordResetOtps");

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

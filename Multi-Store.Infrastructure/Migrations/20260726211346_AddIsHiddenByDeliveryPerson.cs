using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsHiddenByDeliveryPerson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Products_ProductID",
                table: "ChatMessages");

            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenByDeliveryPerson",
                table: "DeliveryAssignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Products_ProductID",
                table: "ChatMessages",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Products_ProductID",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsHiddenByDeliveryPerson",
                table: "DeliveryAssignments");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Products_ProductID",
                table: "ChatMessages",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

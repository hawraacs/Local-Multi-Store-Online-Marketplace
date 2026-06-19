using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryRequestedByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequestedByUserID",
                table: "DeliveryPersons",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPersons_RequestedByUserID",
                table: "DeliveryPersons",
                column: "RequestedByUserID");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryPersons_AspNetUsers_RequestedByUserID",
                table: "DeliveryPersons",
                column: "RequestedByUserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryPersons_AspNetUsers_RequestedByUserID",
                table: "DeliveryPersons");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryPersons_RequestedByUserID",
                table: "DeliveryPersons");

            migrationBuilder.DropColumn(
                name: "RequestedByUserID",
                table: "DeliveryPersons");
        }
    }
}

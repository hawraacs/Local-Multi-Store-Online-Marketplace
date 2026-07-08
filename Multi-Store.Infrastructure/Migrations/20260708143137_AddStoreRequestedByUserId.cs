using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreRequestedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequestedByUserID",
                table: "Stores",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_RequestedByUserID",
                table: "Stores",
                column: "RequestedByUserID",
                unique: true,
                filter: "[RequestedByUserID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_AspNetUsers_RequestedByUserID",
                table: "Stores",
                column: "RequestedByUserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stores_AspNetUsers_RequestedByUserID",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_RequestedByUserID",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "RequestedByUserID",
                table: "Stores");
        }
    }
}

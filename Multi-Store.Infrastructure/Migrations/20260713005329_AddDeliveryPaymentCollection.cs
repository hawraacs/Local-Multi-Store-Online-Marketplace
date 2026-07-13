using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPaymentCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryPaymentCollections",
                columns: table => new
                {
                    CollectionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderID = table.Column<int>(type: "int", nullable: false),
                    DeliveryPersonID = table.Column<int>(type: "int", nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPaymentCollections", x => x.CollectionID);
                    table.ForeignKey(
                        name: "FK_DeliveryPaymentCollections_DeliveryPersons_DeliveryPersonID",
                        column: x => x.DeliveryPersonID,
                        principalTable: "DeliveryPersons",
                        principalColumn: "DeliveryPersonID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryPaymentCollections_Orders_OrderID",
                        column: x => x.OrderID,
                        principalTable: "Orders",
                        principalColumn: "OrderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPaymentCollections_DeliveryPersonID",
                table: "DeliveryPaymentCollections",
                column: "DeliveryPersonID");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPaymentCollections_OrderID",
                table: "DeliveryPaymentCollections",
                column: "OrderID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryPaymentCollections");
        }
    }
}

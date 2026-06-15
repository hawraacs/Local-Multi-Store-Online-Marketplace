using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Multi_Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreSubscriptionAndCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LastPaymentAmount",
                table: "Stores",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaymentDate",
                table: "Stores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionExpiryDate",
                table: "Stores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "Stores",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialStartDate",
                table: "Stores",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPaymentAmount",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "LastPaymentDate",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "SubscriptionExpiryDate",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "TrialStartDate",
                table: "Stores");
        }
    }
}

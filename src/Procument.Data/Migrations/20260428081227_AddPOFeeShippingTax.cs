using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPOFeeShippingTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ProcessingFee",
                table: "PurchaseOrders",
                type: "decimal",
                nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "Shipping",
                table: "PurchaseOrders",
                type: "decimal",
                nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                table: "PurchaseOrders",
                type: "decimal",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingFee",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Shipping",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Tax",
                table: "PurchaseOrders");

        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletSurnameAndFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HandlingFee",
                table: "PaymentBoxes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Surname",
                table: "PaymentBoxes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WireFee",
                table: "PaymentBoxes",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandlingFee",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "Surname",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "WireFee",
                table: "PaymentBoxes");
        }
    }
}

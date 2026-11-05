using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class WalletTransferWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "WalletTransferPendings",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HandlingFee",
                table: "WalletTransferPendings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateBaseCurrency",
                table: "WalletTransferPendings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseBy",
                table: "WalletTransferPendings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WireFee",
                table: "WalletTransferPendings",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandlingFee",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "RateBaseCurrency",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "WarehouseBy",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "WireFee",
                table: "WalletTransferPendings");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "WalletTransferPendings",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);
        }
    }
}

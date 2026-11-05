using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class PopReaderAndWireFeePayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BankFee",
                table: "WalletWithdrawPendings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BocRate",
                table: "WalletWithdrawPendings",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PopAt",
                table: "WalletWithdrawPendings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateAt",
                table: "WalletWithdrawPendings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WireFeePaidBy",
                table: "WalletWithdrawPendings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BankFee",
                table: "WalletTransferPendings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BocRate",
                table: "WalletTransferPendings",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PopAt",
                table: "WalletTransferPendings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateAt",
                table: "WalletTransferPendings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourceWireFee",
                table: "WalletTransferPendings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WireFeePaidBy",
                table: "WalletTransferPendings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarehouseCnyPerUsd",
                table: "WalletSettings",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarehouseRateFetchedAt",
                table: "WalletSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseRatePublishedAt",
                table: "WalletSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WarehouseRateStale",
                table: "WalletSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarehouseRateSyncedAt",
                table: "WalletSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WireFeePaidBy",
                table: "PaymentBoxes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BocRate",
                table: "IncomingDeposits",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateAt",
                table: "IncomingDeposits",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PopReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RefId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    IsPaymentProof = table.Column<bool>(type: "bit", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Fees = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FeesCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TotalDebited = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalDebitedCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReceivedCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    ChargesOption = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ValueDate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PopReadings", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "WalletSettings",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "WarehouseCnyPerUsd", "WarehouseRateFetchedAt", "WarehouseRatePublishedAt", "WarehouseRateStale", "WarehouseRateSyncedAt" },
                values: new object[] { null, null, null, false, null });

            migrationBuilder.CreateIndex(
                name: "IX_PopReadings_Kind_RefId",
                table: "PopReadings",
                columns: new[] { "Kind", "RefId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PopReadings_Status",
                table: "PopReadings",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PopReadings");

            migrationBuilder.DropColumn(
                name: "BankFee",
                table: "WalletWithdrawPendings");

            migrationBuilder.DropColumn(
                name: "BocRate",
                table: "WalletWithdrawPendings");

            migrationBuilder.DropColumn(
                name: "PopAt",
                table: "WalletWithdrawPendings");

            migrationBuilder.DropColumn(
                name: "RateAt",
                table: "WalletWithdrawPendings");

            migrationBuilder.DropColumn(
                name: "WireFeePaidBy",
                table: "WalletWithdrawPendings");

            migrationBuilder.DropColumn(
                name: "BankFee",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "BocRate",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "PopAt",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "RateAt",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "SourceWireFee",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "WireFeePaidBy",
                table: "WalletTransferPendings");

            migrationBuilder.DropColumn(
                name: "WarehouseCnyPerUsd",
                table: "WalletSettings");

            migrationBuilder.DropColumn(
                name: "WarehouseRateFetchedAt",
                table: "WalletSettings");

            migrationBuilder.DropColumn(
                name: "WarehouseRatePublishedAt",
                table: "WalletSettings");

            migrationBuilder.DropColumn(
                name: "WarehouseRateStale",
                table: "WalletSettings");

            migrationBuilder.DropColumn(
                name: "WarehouseRateSyncedAt",
                table: "WalletSettings");

            migrationBuilder.DropColumn(
                name: "WireFeePaidBy",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "BocRate",
                table: "IncomingDeposits");

            migrationBuilder.DropColumn(
                name: "RateAt",
                table: "IncomingDeposits");
        }
    }
}

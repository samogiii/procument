using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class WalletWithdrawWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WalletWithdrawPendings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentBoxId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    OfficeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    ToType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ToSupplierId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentRequestId = table.Column<long>(type: "bigint", nullable: true),
                    Base = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PopFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    HandlingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WireFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WalletAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WarehouseBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    FeeTransactionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletWithdrawPendings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletWithdrawPendings_PaymentBoxes_PaymentBoxId",
                        column: x => x.PaymentBoxId,
                        principalTable: "PaymentBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletWithdrawPendings_Suppliers_ToSupplierId",
                        column: x => x.ToSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawPendings_PaymentBoxId",
                table: "WalletWithdrawPendings",
                column: "PaymentBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawPendings_Status",
                table: "WalletWithdrawPendings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawPendings_ToSupplierId",
                table: "WalletWithdrawPendings",
                column: "ToSupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletWithdrawPendings");
        }
    }
}

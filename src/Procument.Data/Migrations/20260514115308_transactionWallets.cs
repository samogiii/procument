using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class transactionWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WalletTransferPendings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromBoxId = table.Column<long>(type: "bigint", nullable: false),
                    ToBoxId = table.Column<long>(type: "bigint", nullable: false),
                    WithdrawAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PopFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransferPendings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransferPendings_PaymentBoxes_FromBoxId",
                        column: x => x.FromBoxId,
                        principalTable: "PaymentBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransferPendings_PaymentBoxes_ToBoxId",
                        column: x => x.ToBoxId,
                        principalTable: "PaymentBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransferPendings_CreatedAt",
                table: "WalletTransferPendings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransferPendings_FromBoxId",
                table: "WalletTransferPendings",
                column: "FromBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransferPendings_Status",
                table: "WalletTransferPendings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransferPendings_ToBoxId",
                table: "WalletTransferPendings",
                column: "ToBoxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletTransferPendings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseShippingCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarehouseShippingCosts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseShipmentId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TrackNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WarehouseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TrackIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FixedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RejectNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentBoxId = table.Column<long>(type: "bigint", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    WalletAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseShippingCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseShippingCosts_PaymentBoxes_PaymentBoxId",
                        column: x => x.PaymentBoxId,
                        principalTable: "PaymentBoxes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WarehouseShippingCosts_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WarehouseShippingCosts_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShippingCosts_PaymentBoxId",
                table: "WarehouseShippingCosts",
                column: "PaymentBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShippingCosts_PaymentTransactionId",
                table: "WarehouseShippingCosts",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShippingCosts_ReviewedByUserId",
                table: "WarehouseShippingCosts",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShippingCosts_Status",
                table: "WarehouseShippingCosts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShippingCosts_WarehouseShipmentId",
                table: "WarehouseShippingCosts",
                column: "WarehouseShipmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseShippingCosts");
        }
    }
}

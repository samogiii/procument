using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomingDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncomingDeposits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentBoxId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WalletRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    OfficeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankPopFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BankAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BankRate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    BankWalletAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BankBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DepositTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    FeeTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomingDeposits_CustomerPayments_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncomingDeposits_PaymentBoxes_PaymentBoxId",
                        column: x => x.PaymentBoxId,
                        principalTable: "PaymentBoxes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IncomingDeposits_PaymentTransactions_DepositTransactionId",
                        column: x => x.DepositTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IncomingDeposits_PaymentTransactions_FeeTransactionId",
                        column: x => x.FeeTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IncomingDeposits_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomingDeposits_CustomerPaymentId",
                table: "IncomingDeposits",
                column: "CustomerPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomingDeposits_DepositTransactionId",
                table: "IncomingDeposits",
                column: "DepositTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingDeposits_FeeTransactionId",
                table: "IncomingDeposits",
                column: "FeeTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingDeposits_PaymentBoxId",
                table: "IncomingDeposits",
                column: "PaymentBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingDeposits_ReviewedByUserId",
                table: "IncomingDeposits",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingDeposits_Status",
                table: "IncomingDeposits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingDeposits_UploadId",
                table: "IncomingDeposits",
                column: "UploadId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncomingDeposits");
        }
    }
}

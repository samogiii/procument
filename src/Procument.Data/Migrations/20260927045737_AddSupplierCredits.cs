using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierCreditAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyPresetId = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditAccounts", x => x.Id);
                    table.CheckConstraint("CK_SupplierCreditAccounts_Balance", "[Balance] >= 0");
                    table.ForeignKey(
                        name: "FK_SupplierCreditAccounts_CompanyPresets_CompanyPresetId",
                        column: x => x.CompanyPresetId,
                        principalTable: "CompanyPresets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierCreditAccounts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupplierCreditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentRequestId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    ReversesEntryId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditEntries", x => x.Id);
                    table.CheckConstraint("CK_SupplierCreditEntries_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_SupplierCreditEntries_Application", "[Source] <> 'POApplication' OR ([Direction] = 'Out' AND [PurchaseOrderId] IS NOT NULL AND [PaymentRequestId] IS NOT NULL)");
                    table.CheckConstraint("CK_SupplierCreditEntries_BalanceAfter", "[BalanceAfter] >= 0");
                    table.CheckConstraint("CK_SupplierCreditEntries_Direction", "[Direction] IN ('In','Out')");
                    table.CheckConstraint("CK_SupplierCreditEntries_Reversal", "([Source] = 'Reversal' AND [ReversesEntryId] IS NOT NULL) OR ([Source] <> 'Reversal' AND [ReversesEntryId] IS NULL)");
                    table.CheckConstraint("CK_SupplierCreditEntries_Source", "[Source] IN ('CreditNote','WalletTopUp','POApplication','Adjustment','Reversal')");
                    table.CheckConstraint("CK_SupplierCreditEntries_TopUp", "[Source] <> 'WalletTopUp' OR [PaymentTransactionId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_SupplierCreditEntries_PaymentRequests_PaymentRequestId",
                        column: x => x.PaymentRequestId,
                        principalTable: "PaymentRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierCreditEntries_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierCreditEntries_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierCreditEntries_SupplierCreditAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "SupplierCreditAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierCreditEntries_SupplierCreditEntries_ReversesEntryId",
                        column: x => x.ReversesEntryId,
                        principalTable: "SupplierCreditEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierCreditEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditAccounts_CompanyPresetId",
                table: "SupplierCreditAccounts",
                column: "CompanyPresetId")
                .Annotation("SqlServer:Include", new[] { "Balance" });

            migrationBuilder.CreateIndex(
                name: "UX_SupplierCreditAccounts_Supplier_Company_Currency",
                table: "SupplierCreditAccounts",
                columns: new[] { "SupplierId", "CompanyPresetId", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditEntries_Account_CreatedAt",
                table: "SupplierCreditEntries",
                columns: new[] { "AccountId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditEntries_CreatedByUserId",
                table: "SupplierCreditEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditEntries_PaymentRequestId",
                table: "SupplierCreditEntries",
                column: "PaymentRequestId",
                filter: "[PaymentRequestId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "Direction", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditEntries_PaymentTransactionId",
                table: "SupplierCreditEntries",
                column: "PaymentTransactionId",
                filter: "[PaymentTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditEntries_PurchaseOrderId",
                table: "SupplierCreditEntries",
                column: "PurchaseOrderId",
                filter: "[PurchaseOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierCreditEntries_RequestId",
                table: "SupplierCreditEntries",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SupplierCreditEntries_ReversesEntryId",
                table: "SupplierCreditEntries",
                column: "ReversesEntryId",
                unique: true,
                filter: "[ReversesEntryId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierCreditEntries");

            migrationBuilder.DropTable(
                name: "SupplierCreditAccounts");
        }
    }
}

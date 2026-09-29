using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Supplier Credits E10 — credit lines (buy now, pay back later) with the minimum schema change:
    /// SupplierCreditAccounts.Kind + CreditLimit, PaymentRequests.SupplierCreditAccountId (a repayment
    /// is a payment request without a PO), and constraint-only changes on SupplierCreditEntries.
    /// Existing accounts become Kind = 'Prepaid' through the column default.
    /// </summary>
    public partial class AddSupplierCreditLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierCreditEntries_Source",
                table: "SupplierCreditEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierCreditEntries_TopUp",
                table: "SupplierCreditEntries");

            migrationBuilder.DropIndex(
                name: "UX_SupplierCreditAccounts_Supplier_Company_Currency",
                table: "SupplierCreditAccounts");

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "SupplierCreditAccounts",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "SupplierCreditAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Prepaid");

            migrationBuilder.AddColumn<long>(
                name: "SupplierCreditAccountId",
                table: "PaymentRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierCreditEntries_PaymentRequestOnlyOnApplications",
                table: "SupplierCreditEntries",
                sql: "[PaymentRequestId] IS NULL OR [Source] IN ('POApplication','Reversal')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierCreditEntries_Source",
                table: "SupplierCreditEntries",
                sql: "[Source] IN ('CreditNote','WalletTopUp','POApplication','Adjustment','Reversal','CreditLimit','Repayment')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierCreditEntries_TopUp",
                table: "SupplierCreditEntries",
                sql: "[Source] NOT IN ('WalletTopUp','Repayment') OR [PaymentTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierCreditAccounts_Supplier_Company_Currency_Kind",
                table: "SupplierCreditAccounts",
                columns: new[] { "SupplierId", "CompanyPresetId", "Currency", "Kind" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierCreditAccounts_Kind",
                table: "SupplierCreditAccounts",
                sql: "([Kind] = 'Prepaid' AND [CreditLimit] IS NULL) OR ([Kind] = 'CreditLine' AND [CreditLimit] IS NOT NULL AND [Balance] <= [CreditLimit])");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_SupplierCreditAccountId",
                table: "PaymentRequests",
                column: "SupplierCreditAccountId",
                filter: "[SupplierCreditAccountId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_PoOrCreditLine",
                table: "PaymentRequests",
                sql: "[POId] IS NULL OR [SupplierCreditAccountId] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_SupplierCreditAccounts_SupplierCreditAccountId",
                table: "PaymentRequests",
                column: "SupplierCreditAccountId",
                principalTable: "SupplierCreditAccounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Credit lines cannot be squeezed back into "one account per supplier x company x currency";
            // stop with a clear message instead of failing half-way on the old unique index.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [SupplierCreditAccounts] WHERE [Kind] <> 'Prepaid')
                    OR EXISTS (SELECT 1 FROM [PaymentRequests] WHERE [SupplierCreditAccountId] IS NOT NULL)
                    THROW 50000, 'Credit lines exist. Remove them (and their repayment requests) before rolling back AddSupplierCreditLines.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_SupplierCreditAccounts_SupplierCreditAccountId",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierCreditEntries_PaymentRequestOnlyOnApplications",
                table: "SupplierCreditEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierCreditEntries_Source",
                table: "SupplierCreditEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierCreditEntries_TopUp",
                table: "SupplierCreditEntries");

            migrationBuilder.DropIndex(
                name: "UX_SupplierCreditAccounts_Supplier_Company_Currency_Kind",
                table: "SupplierCreditAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierCreditAccounts_Kind",
                table: "SupplierCreditAccounts");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_SupplierCreditAccountId",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_PoOrCreditLine",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "SupplierCreditAccounts");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "SupplierCreditAccounts");

            migrationBuilder.DropColumn(
                name: "SupplierCreditAccountId",
                table: "PaymentRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierCreditEntries_Source",
                table: "SupplierCreditEntries",
                sql: "[Source] IN ('CreditNote','WalletTopUp','POApplication','Adjustment','Reversal')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierCreditEntries_TopUp",
                table: "SupplierCreditEntries",
                sql: "[Source] <> 'WalletTopUp' OR [PaymentTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierCreditAccounts_Supplier_Company_Currency",
                table: "SupplierCreditAccounts",
                columns: new[] { "SupplierId", "CompanyPresetId", "Currency" },
                unique: true);
        }
    }
}

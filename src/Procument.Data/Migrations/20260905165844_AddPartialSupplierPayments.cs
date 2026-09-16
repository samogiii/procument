using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartialSupplierPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompanyPresetId",
                table: "PurchaseOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PopFileName",
                table: "PaymentTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PopInvoiceNumber",
                table: "PaymentTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PopUploadId",
                table: "PaymentTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "PaymentRequests",
                type: "decimal(18,2)",
                nullable: true);

            // Preserve the buying company without treating the old preference as a debit instruction.
            migrationBuilder.Sql(@"UPDATE po SET CompanyPresetId = pb.CompanyPresetId
                FROM PurchaseOrders po JOIN PaymentBoxes pb ON pb.Id = po.PreferredWalletId");
            migrationBuilder.Sql(@"UPDATE pr SET Amount = COALESCE(po.TotalAmount, 0)
                + COALESCE(po.ProcessingFee, 0) + COALESCE(po.Shipping, 0) + COALESCE(po.Tax, 0)
                + COALESCE(detail.Wirefee, 0)
                FROM PaymentRequests pr JOIN PurchaseOrders po ON po.Id = pr.POId
                LEFT JOIN POImportDetails detail ON detail.PurchaseOrderId = po.Id");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PopUploadId",
                table: "PaymentTransactions",
                column: "PopUploadId",
                unique: true,
                filter: "[PopUploadId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_PopUploadId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "CompanyPresetId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PopFileName",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PopInvoiceNumber",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PopUploadId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "PaymentRequests");
        }
    }
}

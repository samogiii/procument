using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceRFQIdWithInvoiceIdOnPO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_RFQs_RFQId",
                table: "PurchaseOrders");

            migrationBuilder.RenameColumn(
                name: "RFQId",
                table: "PurchaseOrders",
                newName: "InvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseOrders_RFQId",
                table: "PurchaseOrders",
                newName: "IX_PurchaseOrders_InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InvoiceId",
                table: "PurchaseOrders",
                newName: "RFQId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseOrders_InvoiceId",
                table: "PurchaseOrders",
                newName: "IX_PurchaseOrders_RFQId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_RFQs_RFQId",
                table: "PurchaseOrders",
                column: "RFQId",
                principalTable: "RFQs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

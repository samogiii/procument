using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOurInventorySalesLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceStockItemId",
                table: "SupplierPartQuote",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FromStock",
                table: "ProcurementItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPartQuote_SourceStockItemId",
                table: "SupplierPartQuote",
                column: "SourceStockItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPartQuote_OurStockItems_SourceStockItemId",
                table: "SupplierPartQuote",
                column: "SourceStockItemId",
                principalTable: "OurStockItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPartQuote_OurStockItems_SourceStockItemId",
                table: "SupplierPartQuote");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPartQuote_SourceStockItemId",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "SourceStockItemId",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "FromStock",
                table: "ProcurementItems");
        }
    }
}

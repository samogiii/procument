using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceSupplierQuoteIdToPendingQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceSupplierQuoteId",
                table: "PendingSupplierQuotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingSupplierQuotes_SourceSupplierQuoteId",
                table: "PendingSupplierQuotes",
                column: "SourceSupplierQuoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingSupplierQuotes_SourceSupplierQuoteId",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "SourceSupplierQuoteId",
                table: "PendingSupplierQuotes");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePendingQuoteIdToSupplierPartQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourcePendingQuoteId",
                table: "SupplierPartQuote",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPartQuote_SourcePendingQuoteId",
                table: "SupplierPartQuote",
                column: "SourcePendingQuoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierPartQuote_SourcePendingQuoteId",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "SourcePendingQuoteId",
                table: "SupplierPartQuote");
        }
    }
}

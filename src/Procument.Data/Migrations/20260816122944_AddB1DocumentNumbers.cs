using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddB1DocumentNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "B1QuoteNumber",
                table: "Quotes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "B1InvoiceNumber",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "B1FinalInvoiceNumber",
                table: "FinalInvoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_B1QuoteNumber",
                table: "Quotes",
                column: "B1QuoteNumber",
                unique: true,
                filter: "[B1QuoteNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_B1InvoiceNumber",
                table: "Invoices",
                column: "B1InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoices_B1FinalInvoiceNumber",
                table: "FinalInvoices",
                column: "B1FinalInvoiceNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quotes_B1QuoteNumber",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_B1InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_FinalInvoices_B1FinalInvoiceNumber",
                table: "FinalInvoices");

            migrationBuilder.DropColumn(
                name: "B1QuoteNumber",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "B1InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "B1FinalInvoiceNumber",
                table: "FinalInvoices");
        }
    }
}

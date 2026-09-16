using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260902000000_RenameBusinessTables")]
public partial class RenameBusinessTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "Invoices", newName: "ProformaInvoices");
        migrationBuilder.RenameTable(name: "InvoiceItems", newName: "ProformaInvoiceItems");
        migrationBuilder.RenameTable(name: "FinalInvoices", newName: "Invoices");
        migrationBuilder.RenameTable(name: "FinalInvoiceItems", newName: "InvoiceItems");
        migrationBuilder.RenameTable(name: "Procument", newName: "SupplierPartQuote");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "SupplierPartQuote", newName: "Procument");
        migrationBuilder.RenameTable(name: "InvoiceItems", newName: "FinalInvoiceItems");
        migrationBuilder.RenameTable(name: "Invoices", newName: "FinalInvoices");
        migrationBuilder.RenameTable(name: "ProformaInvoiceItems", newName: "InvoiceItems");
        migrationBuilder.RenameTable(name: "ProformaInvoices", newName: "Invoices");
    }
}

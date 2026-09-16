using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260908070000_AddCustomerCreditAndInvoiceTerms")]
public partial class AddCustomerCreditAndInvoiceTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "CreditEnabled", table: "Customers", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<decimal>(name: "MaxCredit", table: "Customers", type: "decimal(18,2)", nullable: true);
        migrationBuilder.AddColumn<int>(name: "PaymentTermDays", table: "ProformaInvoices", type: "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "PaymentTermStartedAt", table: "ProformaInvoices", type: "datetime2", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CreditEnabled", table: "Customers");
        migrationBuilder.DropColumn(name: "MaxCredit", table: "Customers");
        migrationBuilder.DropColumn(name: "PaymentTermDays", table: "ProformaInvoices");
        migrationBuilder.DropColumn(name: "PaymentTermStartedAt", table: "ProformaInvoices");
    }
}

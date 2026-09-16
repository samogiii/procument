using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260916090000_AddConditionToProformaInvoiceItems")]
public partial class AddConditionToProformaInvoiceItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Condition",
            table: "ProformaInvoiceItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Condition",
            table: "ProformaInvoiceItems");
    }
}

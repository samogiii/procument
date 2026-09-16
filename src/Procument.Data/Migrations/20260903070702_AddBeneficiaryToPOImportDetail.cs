using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260903070702_AddBeneficiaryToPOImportDetail")]
public partial class AddBeneficiaryToPOImportDetail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Beneficiary",
            table: "POImportDetails",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Beneficiary",
            table: "POImportDetails");
    }
}

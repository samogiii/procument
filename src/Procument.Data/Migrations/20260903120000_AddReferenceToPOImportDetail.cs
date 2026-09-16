using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260903120000_AddReferenceToPOImportDetail")]
public partial class AddReferenceToPOImportDetail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Reference",
            table: "POImportDetails",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Reference",
            table: "POImportDetails");
    }
}

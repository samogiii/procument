using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260917090000_AddSupplierQuoteCertificateReference")]
public partial class AddSupplierQuoteCertificateReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Reference",
            table: "SupplierQuoteCertificates",
            type: "nvarchar(8)",
            maxLength: 8,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE SupplierQuoteCertificates
            SET Reference = UPPER(SUBSTRING(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT(Id, ':', NEWID())), 2), 1, 8));
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Reference",
            table: "SupplierQuoteCertificates",
            type: "nvarchar(8)",
            maxLength: 8,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(8)",
            oldMaxLength: 8,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SupplierQuoteCertificates_Reference",
            table: "SupplierQuoteCertificates",
            column: "Reference",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SupplierQuoteCertificates_Reference",
            table: "SupplierQuoteCertificates");

        migrationBuilder.DropColumn(
            name: "Reference",
            table: "SupplierQuoteCertificates");
    }
}

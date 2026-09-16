using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260906111000_AddSupplierQuoteCertificates")]
public partial class AddSupplierQuoteCertificates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SupplierQuoteCertificates",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SupplierQuoteId = table.Column<long>(type: "bigint", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupplierQuoteCertificates", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupplierQuoteCertificates_SupplierPartQuote_SupplierQuoteId",
                    column: x => x.SupplierQuoteId,
                    principalTable: "SupplierPartQuote",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SupplierQuoteCertificates_Users_UploadedByUserId",
                    column: x => x.UploadedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SupplierQuoteCertificates_SupplierQuoteId",
            table: "SupplierQuoteCertificates",
            column: "SupplierQuoteId");

        migrationBuilder.CreateIndex(
            name: "IX_SupplierQuoteCertificates_UploadedByUserId",
            table: "SupplierQuoteCertificates",
            column: "UploadedByUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "SupplierQuoteCertificates");
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailQuoteExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourcePendingCertificateId",
                table: "SupplierQuoteCertificates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomCertReferences",
                table: "PendingSupplierQuotes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MyNotes",
                table: "PendingSupplierQuotes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellLeadTime",
                table: "PendingSupplierQuotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ShippingCost",
                table: "PendingSupplierQuotes",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingPoint",
                table: "PendingSupplierQuotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingSupplierQuoteCertificates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PendingSupplierQuoteId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingSupplierQuoteCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingSupplierQuoteCertificates_PendingSupplierQuotes_PendingSupplierQuoteId",
                        column: x => x.PendingSupplierQuoteId,
                        principalTable: "PendingSupplierQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingSupplierQuoteCertificates_PendingSupplierQuoteId",
                table: "PendingSupplierQuoteCertificates",
                column: "PendingSupplierQuoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingSupplierQuoteCertificates");

            migrationBuilder.DropColumn(
                name: "SourcePendingCertificateId",
                table: "SupplierQuoteCertificates");

            migrationBuilder.DropColumn(
                name: "CustomCertReferences",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "MyNotes",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "SellLeadTime",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "ShippingPoint",
                table: "PendingSupplierQuotes");
        }
    }
}

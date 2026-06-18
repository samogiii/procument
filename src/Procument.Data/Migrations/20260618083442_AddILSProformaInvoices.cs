using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddILSProformaInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ILSProformaInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PINumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ILSCustomerId = table.Column<long>(type: "bigint", nullable: false),
                    BillTo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ShipTo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CustomerPONumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ILSProformaInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ILSProformaInvoices_ILSCustomers_ILSCustomerId",
                        column: x => x.ILSCustomerId,
                        principalTable: "ILSCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ILSProformaInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ILSProformaInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: false),
                    AltPartNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Qty = table.Column<double>(type: "float", nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LeadTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ILSItemSerialId = table.Column<long>(type: "bigint", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ILSItemId = table.Column<long>(type: "bigint", nullable: true),
                    SourceQuoteId = table.Column<long>(type: "bigint", nullable: true),
                    SourceQuoteItemId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ILSProformaInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ILSProformaInvoiceItems_ILSProformaInvoices_ILSProformaInvoiceId",
                        column: x => x.ILSProformaInvoiceId,
                        principalTable: "ILSProformaInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ILSProformaInvoiceItems_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ILSProformaInvoiceItems_ILSProformaInvoiceId",
                table: "ILSProformaInvoiceItems",
                column: "ILSProformaInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ILSProformaInvoiceItems_PartNumberId",
                table: "ILSProformaInvoiceItems",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_ILSProformaInvoices_CreatedAt",
                table: "ILSProformaInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ILSProformaInvoices_ILSCustomerId",
                table: "ILSProformaInvoices",
                column: "ILSCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ILSProformaInvoiceItems");

            migrationBuilder.DropTable(
                name: "ILSProformaInvoices");
        }
    }
}

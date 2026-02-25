using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalInvoiceAndShippingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Incoterms",
                table: "POImportDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingMethod",
                table: "POImportDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinalInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShippingMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShippingCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProformaInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalInvoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinalInvoices_Invoices_ProformaInvoiceId",
                        column: x => x.ProformaInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinalInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TrackNumber = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FinalInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceItemId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalInvoiceItems_FinalInvoices_FinalInvoiceId",
                        column: x => x.FinalInvoiceId,
                        principalTable: "FinalInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinalInvoiceItems_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoiceItems_FinalInvoiceId",
                table: "FinalInvoiceItems",
                column: "FinalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoiceItems_PartNumberId",
                table: "FinalInvoiceItems",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoices_CreatedAt",
                table: "FinalInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoices_CustomerId",
                table: "FinalInvoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoices_InvoiceNumber",
                table: "FinalInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoices_ProformaInvoiceId",
                table: "FinalInvoices",
                column: "ProformaInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinalInvoiceItems");

            migrationBuilder.DropTable(
                name: "FinalInvoices");

            migrationBuilder.DropColumn(
                name: "Incoterms",
                table: "POImportDetails");

            migrationBuilder.DropColumn(
                name: "ShippingMethod",
                table: "POImportDetails");
        }
    }
}

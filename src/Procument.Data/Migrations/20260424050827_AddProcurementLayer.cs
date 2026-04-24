using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "PurchaseOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReturnedByUserId",
                table: "PurchaseOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "POItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "POItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReturnedFromPOId",
                table: "POItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceProcurementItemId",
                table: "POItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Procurements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcurementNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Open"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Procurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Procurements_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceRfqId = table.Column<long>(type: "bigint", nullable: true),
                    SourceRfqItemId = table.Column<long>(type: "bigint", nullable: true),
                    RfqName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RfqExType = table.Column<int>(type: "int", nullable: true),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: true),
                    PartNumberName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PartNumberDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RfqQty = table.Column<double>(type: "float", nullable: true),
                    RfqCondition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RfqUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RfqPriority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RfqAlt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RfqNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceQuoteId = table.Column<long>(type: "bigint", nullable: true),
                    SourceQuoteItemId = table.Column<long>(type: "bigint", nullable: true),
                    QuoteNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuoteUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuoteQty = table.Column<int>(type: "int", nullable: false),
                    QuoteCondition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuoteAlt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    QuoteLeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    SourceProcumentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    SourceSupplierId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SupplierPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SupplierLeadTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierCondition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierCertName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShippingCost = table.Column<double>(type: "float", nullable: true),
                    SourceInvoiceItemId = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedQty = table.Column<int>(type: "int", nullable: false),
                    AcceptedUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentSupplierId = table.Column<long>(type: "bigint", nullable: true),
                    LeadTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Alt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Open"),
                    LoopCount = table.Column<int>(type: "int", nullable: false),
                    LastReturnReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledByPOItemId = table.Column<long>(type: "bigint", nullable: true),
                    ProcurementId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementItems_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementItems_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementItems_Suppliers_CurrentSupplierId",
                        column: x => x.CurrentSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierQuotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcurementItemId = table.Column<long>(type: "bigint", nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Qty = table.Column<double>(type: "float", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Alt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeadTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShippingCost = table.Column<double>(type: "float", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TagDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ShippingPoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsSelected = table.Column<bool>(type: "bit", nullable: false),
                    SourceProcumentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementSupplierQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierQuotes_ProcurementItems_ProcurementItemId",
                        column: x => x.ProcurementItemId,
                        principalTable: "ProcurementItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierQuotes_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ReturnedAt",
                table: "PurchaseOrders",
                column: "ReturnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_ReturnedAt",
                table: "POItems",
                column: "ReturnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_SourceProcurementItemId",
                table: "POItems",
                column: "SourceProcurementItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_CurrentSupplierId",
                table: "ProcurementItems",
                column: "CurrentSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_ItemStatus",
                table: "ProcurementItems",
                column: "ItemStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_PartNumberId",
                table: "ProcurementItems",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_ProcurementId",
                table: "ProcurementItems",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementItems_SourceInvoiceItemId",
                table: "ProcurementItems",
                column: "SourceInvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_CreatedAt",
                table: "Procurements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_InvoiceId",
                table: "Procurements",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_Status",
                table: "Procurements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierQuotes_ProcurementItemId",
                table: "ProcurementSupplierQuotes",
                column: "ProcurementItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierQuotes_ProcurementItemId_IsSelected",
                table: "ProcurementSupplierQuotes",
                columns: new[] { "ProcurementItemId", "IsSelected" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierQuotes_SupplierId",
                table: "ProcurementSupplierQuotes",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_POItems_ProcurementItems_SourceProcurementItemId",
                table: "POItems",
                column: "SourceProcurementItemId",
                principalTable: "ProcurementItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_POItems_ProcurementItems_SourceProcurementItemId",
                table: "POItems");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierQuotes");

            migrationBuilder.DropTable(
                name: "ProcurementItems");

            migrationBuilder.DropTable(
                name: "Procurements");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_ReturnedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_POItems_ReturnedAt",
                table: "POItems");

            migrationBuilder.DropIndex(
                name: "IX_POItems_SourceProcurementItemId",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReturnedByUserId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "ReturnedFromPOId",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "SourceProcurementItemId",
                table: "POItems");
        }
    }
}

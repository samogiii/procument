using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOurInventoryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceStockItemId",
                table: "QuoteItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DestinationWarehouseId",
                table: "PurchaseOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "PurchaseOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddColumn<string>(
                name: "SupplierPIRef",
                table: "PurchaseOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StockItemId",
                table: "POItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OurStockItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyPresetId = table.Column<long>(type: "bigint", nullable: false),
                    QtyOnHand = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QtyReserved = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QtyAvailable = table.Column<decimal>(type: "decimal(18,2)", nullable: false, computedColumnSql: "[QtyOnHand] - [QtyReserved]", stored: true),
                    AvgUnitCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CertName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TagDate = table.Column<DateTime>(type: "date", nullable: true),
                    BinLocation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MinQty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OurStockItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OurStockItems_CompanyPresets_CompanyPresetId",
                        column: x => x.CompanyPresetId,
                        principalTable: "CompanyPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockItems_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockItems_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    POId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UploadedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDocuments_PurchaseOrders_POId",
                        column: x => x.POId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDocuments_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OurStockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockItemId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    POId = table.Column<long>(type: "bigint", nullable: true),
                    POItemId = table.Column<long>(type: "bigint", nullable: true),
                    TrackNumberId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceItemId = table.Column<long>(type: "bigint", nullable: true),
                    QuoteItemId = table.Column<long>(type: "bigint", nullable: true),
                    RFQItemId = table.Column<long>(type: "bigint", nullable: true),
                    WarehouseTransferId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OurStockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OurStockMovements_OurStockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "OurStockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockMovements_POItemTrackNumbers_TrackNumberId",
                        column: x => x.TrackNumberId,
                        principalTable: "POItemTrackNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockMovements_POItems_POItemId",
                        column: x => x.POItemId,
                        principalTable: "POItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockMovements_PurchaseOrders_POId",
                        column: x => x.POId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockMovements_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockMovements_WarehouseTransfers_WarehouseTransferId",
                        column: x => x.WarehouseTransferId,
                        principalTable: "WarehouseTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OurStockReservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockItemId = table.Column<long>(type: "bigint", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RFQItemId = table.Column<long>(type: "bigint", nullable: true),
                    QuoteItemId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceItemId = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OurStockReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OurStockReservations_OurStockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "OurStockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockReservations_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OurStockSerials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockItemId = table.Column<long>(type: "bigint", nullable: false),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceiptMovementId = table.Column<long>(type: "bigint", nullable: true),
                    IssueMovementId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OurStockSerials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OurStockSerials_OurStockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "OurStockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockSerials_OurStockMovements_IssueMovementId",
                        column: x => x.IssueMovementId,
                        principalTable: "OurStockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockSerials_OurStockMovements_ReceiptMovementId",
                        column: x => x.ReceiptMovementId,
                        principalTable: "OurStockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OurStockSerials_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteItems_SourceStockItemId",
                table: "QuoteItems",
                column: "SourceStockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_DestinationWarehouseId",
                table: "PurchaseOrders",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Origin_Status",
                table: "PurchaseOrders",
                columns: new[] { "Origin", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_POItems_StockItemId",
                table: "POItems",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockItems_CompanyPresetId",
                table: "OurStockItems",
                column: "CompanyPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockItems_PartNumberId",
                table: "OurStockItems",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockItems_PartNumberId_Condition_WarehouseId_CompanyPresetId_CertName",
                table: "OurStockItems",
                columns: new[] { "PartNumberId", "Condition", "WarehouseId", "CompanyPresetId", "CertName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OurStockItems_WarehouseId",
                table: "OurStockItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockMovements_CreatedBy",
                table: "OurStockMovements",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockMovements_InvoiceItemId",
                table: "OurStockMovements",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockMovements_POId",
                table: "OurStockMovements",
                column: "POId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockMovements_POItemId",
                table: "OurStockMovements",
                column: "POItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockMovements_StockItemId_CreatedAt",
                table: "OurStockMovements",
                columns: new[] { "StockItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OurStockMovements_TrackNumberId_POItemId",
                table: "OurStockMovements",
                columns: new[] { "TrackNumberId", "POItemId" },
                unique: true,
                filter: "[TrackNumberId] IS NOT NULL AND [POItemId] IS NOT NULL AND [Type] = 'Receipt'");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockMovements_WarehouseTransferId",
                table: "OurStockMovements",
                column: "WarehouseTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockReservations_CreatedBy",
                table: "OurStockReservations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockReservations_ExpiresAt",
                table: "OurStockReservations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockReservations_InvoiceItemId",
                table: "OurStockReservations",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockReservations_StockItemId_Status",
                table: "OurStockReservations",
                columns: new[] { "StockItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OurStockSerials_IssueMovementId",
                table: "OurStockSerials",
                column: "IssueMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockSerials_PartNumberId_SerialNumber",
                table: "OurStockSerials",
                columns: new[] { "PartNumberId", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OurStockSerials_ReceiptMovementId",
                table: "OurStockSerials",
                column: "ReceiptMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_OurStockSerials_StockItemId",
                table: "OurStockSerials",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDocuments_POId",
                table: "PurchaseOrderDocuments",
                column: "POId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDocuments_POId_Category",
                table: "PurchaseOrderDocuments",
                columns: new[] { "POId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDocuments_UploadedBy",
                table: "PurchaseOrderDocuments",
                column: "UploadedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_POItems_OurStockItems_StockItemId",
                table: "POItems",
                column: "StockItemId",
                principalTable: "OurStockItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Warehouses_DestinationWarehouseId",
                table: "PurchaseOrders",
                column: "DestinationWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteItems_OurStockItems_SourceStockItemId",
                table: "QuoteItems",
                column: "SourceStockItemId",
                principalTable: "OurStockItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_POItems_OurStockItems_StockItemId",
                table: "POItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Warehouses_DestinationWarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteItems_OurStockItems_SourceStockItemId",
                table: "QuoteItems");

            migrationBuilder.DropTable(
                name: "OurStockReservations");

            migrationBuilder.DropTable(
                name: "OurStockSerials");

            migrationBuilder.DropTable(
                name: "PurchaseOrderDocuments");

            migrationBuilder.DropTable(
                name: "OurStockMovements");

            migrationBuilder.DropTable(
                name: "OurStockItems");

            migrationBuilder.DropIndex(
                name: "IX_QuoteItems_SourceStockItemId",
                table: "QuoteItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_DestinationWarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_Origin_Status",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_POItems_StockItemId",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "SourceStockItemId",
                table: "QuoteItems");

            migrationBuilder.DropColumn(
                name: "DestinationWarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierPIRef",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "StockItemId",
                table: "POItems");
        }
    }
}

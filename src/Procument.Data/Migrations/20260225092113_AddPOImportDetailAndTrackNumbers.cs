using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPOImportDetailAndTrackNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "POImportDetails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BankCity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankCountry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FedExAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CourierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POImportDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POImportDetails_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POItemTrackNumbers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    POItemId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POItemTrackNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POItemTrackNumbers_POItems_POItemId",
                        column: x => x.POItemId,
                        principalTable: "POItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POImportDetails_PurchaseOrderId",
                table: "POImportDetails",
                column: "PurchaseOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POItemTrackNumbers_POItemId",
                table: "POItemTrackNumbers",
                column: "POItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "POImportDetails");

            migrationBuilder.DropTable(
                name: "POItemTrackNumbers");
        }
    }
}

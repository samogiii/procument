using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransferredOutQty",
                table: "TrackNumberItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "POItemTrackNumbers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Supplier");

            migrationBuilder.AddColumn<long>(
                name: "ParentTrackNumberId",
                table: "POItemTrackNumbers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceTransferId",
                table: "POItemTrackNumbers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WarehouseTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromWarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ToWarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    TrackNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "In Transit"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Warehouses_FromWarehouseId",
                        column: x => x.FromWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Warehouses_ToWarehouseId",
                        column: x => x.ToWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseTransferItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseTransferId = table.Column<long>(type: "bigint", nullable: false),
                    SourceTrackNumberItemId = table.Column<long>(type: "bigint", nullable: false),
                    POItemId = table.Column<long>(type: "bigint", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    ReceivedQty = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "In Transit"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseTransferItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseTransferItems_POItems_POItemId",
                        column: x => x.POItemId,
                        principalTable: "POItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransferItems_TrackNumberItems_SourceTrackNumberItemId",
                        column: x => x.SourceTrackNumberItemId,
                        principalTable: "TrackNumberItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransferItems_WarehouseTransfers_WarehouseTransferId",
                        column: x => x.WarehouseTransferId,
                        principalTable: "WarehouseTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POItemTrackNumbers_ParentTrackNumberId",
                table: "POItemTrackNumbers",
                column: "ParentTrackNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_POItemTrackNumbers_SourceTransferId",
                table: "POItemTrackNumbers",
                column: "SourceTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransferItems_POItemId",
                table: "WarehouseTransferItems",
                column: "POItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransferItems_SourceTrackNumberItemId",
                table: "WarehouseTransferItems",
                column: "SourceTrackNumberItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransferItems_WarehouseTransferId",
                table: "WarehouseTransferItems",
                column: "WarehouseTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_CreatedByUserId",
                table: "WarehouseTransfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_FromWarehouseId",
                table: "WarehouseTransfers",
                column: "FromWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_ReceivedByUserId",
                table: "WarehouseTransfers",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_Status",
                table: "WarehouseTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_ToWarehouseId",
                table: "WarehouseTransfers",
                column: "ToWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_TransferNumber",
                table: "WarehouseTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_POItemTrackNumbers_POItemTrackNumbers_ParentTrackNumberId",
                table: "POItemTrackNumbers",
                column: "ParentTrackNumberId",
                principalTable: "POItemTrackNumbers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POItemTrackNumbers_WarehouseTransfers_SourceTransferId",
                table: "POItemTrackNumbers",
                column: "SourceTransferId",
                principalTable: "WarehouseTransfers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_POItemTrackNumbers_POItemTrackNumbers_ParentTrackNumberId",
                table: "POItemTrackNumbers");

            migrationBuilder.DropForeignKey(
                name: "FK_POItemTrackNumbers_WarehouseTransfers_SourceTransferId",
                table: "POItemTrackNumbers");

            migrationBuilder.DropTable(
                name: "WarehouseTransferItems");

            migrationBuilder.DropTable(
                name: "WarehouseTransfers");

            migrationBuilder.DropIndex(
                name: "IX_POItemTrackNumbers_ParentTrackNumberId",
                table: "POItemTrackNumbers");

            migrationBuilder.DropIndex(
                name: "IX_POItemTrackNumbers_SourceTransferId",
                table: "POItemTrackNumbers");

            migrationBuilder.DropColumn(
                name: "TransferredOutQty",
                table: "TrackNumberItems");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "POItemTrackNumbers");

            migrationBuilder.DropColumn(
                name: "ParentTrackNumberId",
                table: "POItemTrackNumbers");

            migrationBuilder.DropColumn(
                name: "SourceTransferId",
                table: "POItemTrackNumbers");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryWarehouseShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "POItemTrackNumbers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<long>(
                name: "WarehouseId",
                table: "POItemTrackNumbers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrackNumberDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackNumberId = table.Column<long>(type: "bigint", nullable: false),
                    POItemId = table.Column<long>(type: "bigint", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackNumberDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackNumberDocuments_POItemTrackNumbers_TrackNumberId",
                        column: x => x.TrackNumberId,
                        principalTable: "POItemTrackNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackNumberDocuments_POItems_POItemId",
                        column: x => x.POItemId,
                        principalTable: "POItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrackNumberDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrackNumberItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackNumberId = table.Column<long>(type: "bigint", nullable: false),
                    POItemId = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedQty = table.Column<int>(type: "int", nullable: false),
                    ActualQty = table.Column<int>(type: "int", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackNumberItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackNumberItems_POItemTrackNumbers_TrackNumberId",
                        column: x => x.TrackNumberId,
                        principalTable: "POItemTrackNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackNumberItems_POItems_POItemId",
                        column: x => x.POItemId,
                        principalTable: "POItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrackNumberItems_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "OurWarehouse"),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyPresetWarehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyPresetId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPresetWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPresetWarehouses_CompanyPresets_CompanyPresetId",
                        column: x => x.CompanyPresetId,
                        principalTable: "CompanyPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyPresetWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SNNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    TId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AWBNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PdfFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipmentNotes_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserWarehouses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWarehouses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentNoteTrackNumbers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentNoteId = table.Column<long>(type: "bigint", nullable: false),
                    TrackNumberId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentNoteTrackNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentNoteTrackNumbers_POItemTrackNumbers_TrackNumberId",
                        column: x => x.TrackNumberId,
                        principalTable: "POItemTrackNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipmentNoteTrackNumbers_ShipmentNotes_ShipmentNoteId",
                        column: x => x.ShipmentNoteId,
                        principalTable: "ShipmentNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POItemTrackNumbers_Status",
                table: "POItemTrackNumbers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_POItemTrackNumbers_WarehouseId",
                table: "POItemTrackNumbers",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPresetWarehouses_CompanyPresetId_WarehouseId",
                table: "CompanyPresetWarehouses",
                columns: new[] { "CompanyPresetId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPresetWarehouses_WarehouseId",
                table: "CompanyPresetWarehouses",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNotes_CreatedByUserId",
                table: "ShipmentNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNotes_SNNumber",
                table: "ShipmentNotes",
                column: "SNNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNotes_Status",
                table: "ShipmentNotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNotes_WarehouseId",
                table: "ShipmentNotes",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteTrackNumbers_ShipmentNoteId_TrackNumberId",
                table: "ShipmentNoteTrackNumbers",
                columns: new[] { "ShipmentNoteId", "TrackNumberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteTrackNumbers_TrackNumberId",
                table: "ShipmentNoteTrackNumbers",
                column: "TrackNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberDocuments_POItemId",
                table: "TrackNumberDocuments",
                column: "POItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberDocuments_TrackNumberId_POItemId",
                table: "TrackNumberDocuments",
                columns: new[] { "TrackNumberId", "POItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberDocuments_UploadedByUserId",
                table: "TrackNumberDocuments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberItems_POItemId",
                table: "TrackNumberItems",
                column: "POItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberItems_ReviewedByUserId",
                table: "TrackNumberItems",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberItems_Status",
                table: "TrackNumberItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberItems_TrackNumberId_POItemId",
                table: "TrackNumberItems",
                columns: new[] { "TrackNumberId", "POItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouses_UserId_WarehouseId",
                table: "UserWarehouses",
                columns: new[] { "UserId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouses_WarehouseId",
                table: "UserWarehouses",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_IsActive",
                table: "Warehouses",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_POItemTrackNumbers_Warehouses_WarehouseId",
                table: "POItemTrackNumbers",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_POItemTrackNumbers_Warehouses_WarehouseId",
                table: "POItemTrackNumbers");

            migrationBuilder.DropTable(
                name: "CompanyPresetWarehouses");

            migrationBuilder.DropTable(
                name: "ShipmentNoteTrackNumbers");

            migrationBuilder.DropTable(
                name: "TrackNumberDocuments");

            migrationBuilder.DropTable(
                name: "TrackNumberItems");

            migrationBuilder.DropTable(
                name: "UserWarehouses");

            migrationBuilder.DropTable(
                name: "ShipmentNotes");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_POItemTrackNumbers_Status",
                table: "POItemTrackNumbers");

            migrationBuilder.DropIndex(
                name: "IX_POItemTrackNumbers_WarehouseId",
                table: "POItemTrackNumbers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "POItemTrackNumbers");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "POItemTrackNumbers");
        }
    }
}

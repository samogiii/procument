using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentNotePickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PickupNote",
                table: "ShipmentNotes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShipmentNoteDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentNoteId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentNoteDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentNoteDocuments_ShipmentNotes_ShipmentNoteId",
                        column: x => x.ShipmentNoteId,
                        principalTable: "ShipmentNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentNoteDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteDocuments_ShipmentNoteId",
                table: "ShipmentNoteDocuments",
                column: "ShipmentNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteDocuments_UploadedByUserId",
                table: "ShipmentNoteDocuments",
                column: "UploadedByUserId");

            // The Internal / External Shipping menus are replaced by per-user base grants.
            // Carry the existing grants over: Internal → Base 1, External → Bases 2–5.
            migrationBuilder.Sql("""
                INSERT INTO MenuPermissions (Feature, UserName, CreatedAt)
                SELECT v.Feature, p.UserName, SYSUTCDATETIME()
                FROM MenuPermissions p
                JOIN (VALUES ('internalShipping', 'shippingBase1'),
                             ('externalShipping', 'shippingBase2'),
                             ('externalShipping', 'shippingBase3'),
                             ('externalShipping', 'shippingBase4'),
                             ('externalShipping', 'shippingBase5')) v(OldFeature, Feature)
                  ON v.OldFeature = p.Feature
                WHERE NOT EXISTS (SELECT 1 FROM MenuPermissions x WHERE x.Feature = v.Feature AND x.UserName = p.UserName);

                DELETE FROM MenuPermissions WHERE Feature IN ('internalShipping', 'externalShipping');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentNoteDocuments");

            migrationBuilder.DropColumn(
                name: "PickupNote",
                table: "ShipmentNotes");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class boxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ShipmentNotes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "CustomsFileName",
                table: "ShipmentNotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomsOriginalFileName",
                table: "ShipmentNotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomsUploadedAt",
                table: "ShipmentNotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "ShipmentNotes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "DDP");

            migrationBuilder.CreateTable(
                name: "ShipmentNoteBoxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentNoteId = table.Column<long>(type: "bigint", nullable: false),
                    BoxNumber = table.Column<int>(type: "int", nullable: false),
                    TrackNumberId = table.Column<long>(type: "bigint", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(10,3)", nullable: true),
                    HeightCm = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    WidthCm = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LengthCm = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentNoteBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentNoteBoxes_POItemTrackNumbers_TrackNumberId",
                        column: x => x.TrackNumberId,
                        principalTable: "POItemTrackNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShipmentNoteBoxes_ShipmentNotes_ShipmentNoteId",
                        column: x => x.ShipmentNoteId,
                        principalTable: "ShipmentNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackNumberBoxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackNumberId = table.Column<long>(type: "bigint", nullable: false),
                    BoxNumber = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(10,3)", nullable: true),
                    HeightCm = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    WidthCm = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LengthCm = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackNumberBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackNumberBoxes_POItemTrackNumbers_TrackNumberId",
                        column: x => x.TrackNumberId,
                        principalTable: "POItemTrackNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteBoxes_ShipmentNoteId",
                table: "ShipmentNoteBoxes",
                column: "ShipmentNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteBoxes_TrackNumberId",
                table: "ShipmentNoteBoxes",
                column: "TrackNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackNumberBoxes_TrackNumberId",
                table: "TrackNumberBoxes",
                column: "TrackNumberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentNoteBoxes");

            migrationBuilder.DropTable(
                name: "TrackNumberBoxes");

            migrationBuilder.DropColumn(
                name: "CustomsFileName",
                table: "ShipmentNotes");

            migrationBuilder.DropColumn(
                name: "CustomsOriginalFileName",
                table: "ShipmentNotes");

            migrationBuilder.DropColumn(
                name: "CustomsUploadedAt",
                table: "ShipmentNotes");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ShipmentNotes");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ShipmentNotes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Draft");
        }
    }
}

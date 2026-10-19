using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPOItemToShipmentNoteDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "POItemId",
                table: "ShipmentNoteDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteDocuments_POItemId",
                table: "ShipmentNoteDocuments",
                column: "POItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentNoteDocuments_ShipmentNoteId_POItemId",
                table: "ShipmentNoteDocuments",
                columns: new[] { "ShipmentNoteId", "POItemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentNoteDocuments_POItems_POItemId",
                table: "ShipmentNoteDocuments",
                column: "POItemId",
                principalTable: "POItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentNoteDocuments_POItems_POItemId",
                table: "ShipmentNoteDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentNoteDocuments_POItemId",
                table: "ShipmentNoteDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentNoteDocuments_ShipmentNoteId_POItemId",
                table: "ShipmentNoteDocuments");

            migrationBuilder.DropColumn(
                name: "POItemId",
                table: "ShipmentNoteDocuments");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShippingItemsSOAndDestinatioAndCertNeeded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CertNeeded",
                table: "TrackNumberItems",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "ShipmentNotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SONumber",
                table: "ShipmentNotes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertNeeded",
                table: "TrackNumberItems");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "ShipmentNotes");

            migrationBuilder.DropColumn(
                name: "SONumber",
                table: "ShipmentNotes");
        }
    }
}

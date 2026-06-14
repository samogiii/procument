using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestampsToProcumentRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Procument",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Procument",
                type: "datetime2",
                nullable: true);

            // Backfill existing rows: these have no real creation timestamp, so approximate it
            // with the deadline (LeadTime) of the RFQ the cost was gathered for — the closest
            // signal available for when that price was relevant.
            migrationBuilder.Sql(@"
                UPDATE p
                SET p.CreatedAt = r.LeadTime
                FROM Procument p
                INNER JOIN RFQItems ri ON p.RFQItemId = ri.Id
                INNER JOIN RFQs r ON ri.RFQId = r.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Procument");
        }
    }
}

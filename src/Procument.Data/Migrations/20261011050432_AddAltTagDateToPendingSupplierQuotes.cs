using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAltTagDateToPendingSupplierQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alt",
                table: "PendingSupplierQuotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TagDate",
                table: "PendingSupplierQuotes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagDateRaw",
                table: "PendingSupplierQuotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alt",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "TagDate",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "TagDateRaw",
                table: "PendingSupplierQuotes");
        }
    }
}

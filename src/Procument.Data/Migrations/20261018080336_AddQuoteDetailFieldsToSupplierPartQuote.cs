using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteDetailFieldsToSupplierPartQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MoQ",
                table: "SupplierPartQuote",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                table: "SupplierPartQuote",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "SupplierPartQuote",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagType",
                table: "SupplierPartQuote",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaggedBy",
                table: "SupplierPartQuote",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceTo",
                table: "SupplierPartQuote",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Warranty",
                table: "SupplierPartQuote",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoQ",
                table: "PendingSupplierQuotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                table: "PendingSupplierQuotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "PendingSupplierQuotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagType",
                table: "PendingSupplierQuotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaggedBy",
                table: "PendingSupplierQuotes",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceTo",
                table: "PendingSupplierQuotes",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Warranty",
                table: "PendingSupplierQuotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoQ",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "PriceType",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "TagType",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "TaggedBy",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "TraceTo",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "MoQ",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "PriceType",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "TagType",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "TaggedBy",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "TraceTo",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "PendingSupplierQuotes");
        }
    }
}

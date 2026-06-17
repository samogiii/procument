using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialFieldsToILSQuoteItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "ILSQuoteItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Coef",
                table: "ILSQuoteItems",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ILSItemSerialId",
                table: "ILSQuoteItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "ILSQuoteItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "ILSQuoteItems");

            migrationBuilder.DropColumn(
                name: "Coef",
                table: "ILSQuoteItems");

            migrationBuilder.DropColumn(
                name: "ILSItemSerialId",
                table: "ILSQuoteItems");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "ILSQuoteItems");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIlsCustomerAndQuoteAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillTo",
                table: "ILSQuotes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo",
                table: "ILSQuotes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillTo",
                table: "ILSCustomers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "ILSCustomers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo",
                table: "ILSCustomers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAccount",
                table: "ILSCustomers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "ILSCustomers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "ILSCustomers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillTo",
                table: "ILSQuotes");

            migrationBuilder.DropColumn(
                name: "ShipTo",
                table: "ILSQuotes");

            migrationBuilder.DropColumn(
                name: "BillTo",
                table: "ILSCustomers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "ILSCustomers");

            migrationBuilder.DropColumn(
                name: "ShipTo",
                table: "ILSCustomers");

            migrationBuilder.DropColumn(
                name: "ShippingAccount",
                table: "ILSCustomers");

            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "ILSCustomers");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "ILSCustomers");
        }
    }
}

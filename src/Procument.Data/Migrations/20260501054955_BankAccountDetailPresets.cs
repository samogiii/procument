using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class BankAccountDetailPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAddress",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeneficiaryName",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwiftCode",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "BankAddress",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "BeneficiaryName",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SwiftCode",
                table: "CompanyPresets");
        }
    }
}

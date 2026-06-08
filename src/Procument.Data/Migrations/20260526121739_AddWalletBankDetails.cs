using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "PaymentBoxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAddress",
                table: "PaymentBoxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "PaymentBoxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BeneficiaryName",
                table: "PaymentBoxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SwiftCode",
                table: "PaymentBoxes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "BankAddress",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "BeneficiaryName",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "SwiftCode",
                table: "PaymentBoxes");
        }
    }
}

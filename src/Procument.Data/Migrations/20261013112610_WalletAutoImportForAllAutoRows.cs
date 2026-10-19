using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class WalletAutoImportForAllAutoRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AutoImportDeposits",
                table: "WalletSettings",
                newName: "AutoImport");

            migrationBuilder.AddColumn<bool>(
                name: "InWallet",
                table: "PaymentTransactions",
                type: "bit",
                nullable: false,
                defaultValue: true);   // every existing row already counts in its wallet
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InWallet",
                table: "PaymentTransactions");

            migrationBuilder.RenameColumn(
                name: "AutoImport",
                table: "WalletSettings",
                newName: "AutoImportDeposits");
        }
    }
}

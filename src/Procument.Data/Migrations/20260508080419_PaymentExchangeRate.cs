using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentExchangeRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PaymentTransactions",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ToPaymentBoxId",
                table: "PaymentTransactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TxCurrency",
                table: "PaymentTransactions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ToPaymentBoxId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "TxCurrency",
                table: "PaymentTransactions");
        }
    }
}

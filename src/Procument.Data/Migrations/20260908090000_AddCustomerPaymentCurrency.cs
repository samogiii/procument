using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260908090000_AddCustomerPaymentCurrency")]
public partial class AddCustomerPaymentCurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Currency", table: "CustomerPayments", type: "nvarchar(10)", maxLength: 10, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "ExchangeRate", table: "CustomerPayments", type: "decimal(18,8)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "ReceivedAmount", table: "CustomerPayments", type: "decimal(18,2)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Currency", table: "CustomerPayments");
        migrationBuilder.DropColumn(name: "ExchangeRate", table: "CustomerPayments");
        migrationBuilder.DropColumn(name: "ReceivedAmount", table: "CustomerPayments");
    }
}

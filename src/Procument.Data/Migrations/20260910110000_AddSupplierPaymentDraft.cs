using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260910110000_AddSupplierPaymentDraft")]
public partial class AddSupplierPaymentDraft : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "PaymentStatus",
            table: "PurchaseOrders",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "NotStarted",
            oldClrType: typeof(string),
            oldType: "nvarchar(20)",
            oldMaxLength: 20,
            oldDefaultValue: "NotStarted");

        migrationBuilder.AddColumn<long>(
            name: "PendingWalletId",
            table: "PaymentRequests",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PendingPaymentAmount",
            table: "PaymentRequests",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PendingExchangeRate",
            table: "PaymentRequests",
            type: "decimal(18,6)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PendingUploadId",
            table: "PaymentRequests",
            type: "uniqueidentifier",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PendingWalletId", table: "PaymentRequests");
        migrationBuilder.DropColumn(name: "PendingPaymentAmount", table: "PaymentRequests");
        migrationBuilder.DropColumn(name: "PendingExchangeRate", table: "PaymentRequests");
        migrationBuilder.DropColumn(name: "PendingUploadId", table: "PaymentRequests");

        migrationBuilder.AlterColumn<string>(
            name: "PaymentStatus",
            table: "PurchaseOrders",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "NotStarted",
            oldClrType: typeof(string),
            oldType: "nvarchar(40)",
            oldMaxLength: 40,
            oldDefaultValue: "NotStarted");
    }
}

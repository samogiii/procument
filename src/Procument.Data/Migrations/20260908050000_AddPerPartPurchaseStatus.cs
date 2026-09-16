using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Procument.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260908050000_AddPerPartPurchaseStatus")]
public partial class AddPerPartPurchaseStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Status", table: "ProformaInvoiceItems", type: "nvarchar(80)",
            maxLength: 80, nullable: false, defaultValue: "Not Started");
        migrationBuilder.AddColumn<string>(
            name: "FulfillmentMode", table: "PurchaseOrders", type: "nvarchar(20)",
            maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "InShopLeadTimeDays", table: "POItems", type: "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "InShopStartedAt", table: "POItems", type: "datetime2", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Status", table: "ProformaInvoiceItems");
        migrationBuilder.DropColumn(name: "FulfillmentMode", table: "PurchaseOrders");
        migrationBuilder.DropColumn(name: "InShopLeadTimeDays", table: "POItems");
        migrationBuilder.DropColumn(name: "InShopStartedAt", table: "POItems");
    }
}

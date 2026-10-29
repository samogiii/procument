using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehousePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WarehouseBankWireFee",
                table: "PaymentRequests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarehouseHandlingFee",
                table: "PaymentRequests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarehouseExchangeRate",
                table: "PaymentRequests",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarehouseFinalAmount",
                table: "PaymentRequests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarehousePaidAt",
                table: "PaymentRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehousePaidBy",
                table: "PaymentRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehousePopFileName",
                table: "PaymentRequests",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseRejectNote",
                table: "PaymentRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarehouseBankWireFee",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "WarehouseHandlingFee",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "WarehouseExchangeRate",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "WarehouseFinalAmount",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "WarehousePaidAt",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "WarehousePaidBy",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "WarehousePopFileName",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "WarehouseRejectNote",
                table: "PaymentRequests");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentApproval",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentApprovalAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PaymentApprovalBy",
                table: "PurchaseOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentApprovalNote",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentApproval",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentApprovalAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentApprovalBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentApprovalNote",
                table: "PurchaseOrders");
        }
    }
}

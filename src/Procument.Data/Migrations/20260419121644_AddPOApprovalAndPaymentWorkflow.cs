using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPOApprovalAndPaymentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminApproval",
                table: "PurchaseOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminApprovalAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AdminApprovalBy",
                table: "PurchaseOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminApprovalNote",
                table: "PurchaseOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "PurchaseOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentSubmittedAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PaymentSubmittedBy",
                table: "PurchaseOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_AdminApproval",
                table: "PurchaseOrders",
                column: "AdminApproval");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PaymentStatus",
                table: "PurchaseOrders",
                column: "PaymentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_AdminApproval",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PaymentStatus",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AdminApproval",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AdminApprovalAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AdminApprovalBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AdminApprovalNote",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentSubmittedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentSubmittedBy",
                table: "PurchaseOrders");
        }
    }
}

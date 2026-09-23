using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletTransactionReviewAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OriginalExchangeRate",
                table: "PaymentTransactions",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RateEditedAt",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RateEditedByUserId",
                table: "PaymentTransactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReviewedByUserId",
                table: "PaymentTransactions",
                type: "bigint",
                nullable: true);

            // Existing automatic transactions predate the review workflow and must not
            // all appear as new on deployment. Only future auto rows start unreviewed.
            migrationBuilder.Sql("""
                UPDATE [PaymentTransactions]
                SET [ReviewedAt] = [CreatedAt]
                WHERE [IsAuto] = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_RateEditedByUserId",
                table: "PaymentTransactions",
                column: "RateEditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ReviewedByUserId",
                table: "PaymentTransactions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Unreviewed",
                table: "PaymentTransactions",
                columns: new[] { "IsAuto", "ReviewedAt" },
                filter: "[ReviewedAt] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Users_RateEditedByUserId",
                table: "PaymentTransactions",
                column: "RateEditedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Users_ReviewedByUserId",
                table: "PaymentTransactions",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Users_RateEditedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Users_ReviewedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_RateEditedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_ReviewedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_Unreviewed",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "OriginalExchangeRate",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RateEditedAt",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RateEditedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "PaymentTransactions");
        }
    }
}

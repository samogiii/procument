using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletSubWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ParentBoxId",
                table: "PaymentBoxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBoxes_ParentBoxId",
                table: "PaymentBoxes",
                column: "ParentBoxId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentBoxes_PaymentBoxes_ParentBoxId",
                table: "PaymentBoxes",
                column: "ParentBoxId",
                principalTable: "PaymentBoxes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentBoxes_PaymentBoxes_ParentBoxId",
                table: "PaymentBoxes");

            migrationBuilder.DropIndex(
                name: "IX_PaymentBoxes_ParentBoxId",
                table: "PaymentBoxes");

            migrationBuilder.DropColumn(
                name: "ParentBoxId",
                table: "PaymentBoxes");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExTypeAndNullablePOId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_POItems_PurchaseOrders_POId",
                table: "POItems");

            migrationBuilder.AddColumn<int>(
                name: "ExType",
                table: "RFQs",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "POId",
                table: "POItems",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "InvoiceItemId",
                table: "POItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_POItems_PurchaseOrders_POId",
                table: "POItems",
                column: "POId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_POItems_PurchaseOrders_POId",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "ExType",
                table: "RFQs");

            migrationBuilder.DropColumn(
                name: "InvoiceItemId",
                table: "POItems");

            migrationBuilder.AlterColumn<long>(
                name: "POId",
                table: "POItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_POItems_PurchaseOrders_POId",
                table: "POItems",
                column: "POId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

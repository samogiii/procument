using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EntityPermissions_UserId",
                table: "EntityPermissions");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Suppliers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Suppliers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RFQs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Status",
                table: "Suppliers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Username",
                table: "Suppliers",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_RFQs_UserId_Status",
                table: "RFQs",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteItems_Alt",
                table: "QuoteItems",
                column: "Alt");

            migrationBuilder.CreateIndex(
                name: "IX_PartNumbers_Name",
                table: "PartNumbers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EntityPermissions_UserId_EntityName",
                table: "EntityPermissions",
                columns: new[] { "UserId", "EntityName" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Status",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Username",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_RFQs_UserId_Status",
                table: "RFQs");

            migrationBuilder.DropIndex(
                name: "IX_QuoteItems_Alt",
                table: "QuoteItems");

            migrationBuilder.DropIndex(
                name: "IX_PartNumbers_Name",
                table: "PartNumbers");

            migrationBuilder.DropIndex(
                name: "IX_EntityPermissions_UserId_EntityName",
                table: "EntityPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Name",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RFQs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_EntityPermissions_UserId",
                table: "EntityPermissions",
                column: "UserId");
        }
    }
}

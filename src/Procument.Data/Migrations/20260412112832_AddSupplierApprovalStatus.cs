using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RequestedByUserId",
                table: "Suppliers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Suppliers",
                type: "nvarchar(50)",
                nullable: false,
                defaultValue: "Approved");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Suppliers");
        }
    }
}

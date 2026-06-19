using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectToPurchaseOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subject",
                table: "PurchaseOrders");
        }
    }
}

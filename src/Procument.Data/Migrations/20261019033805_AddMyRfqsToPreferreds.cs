using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMyRfqsToPreferreds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RfqId",
                table: "Preferreds",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Preferreds_Kind_UserId_RfqId",
                table: "Preferreds",
                columns: new[] { "Kind", "UserId", "RfqId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Preferreds_Kind_UserId_RfqId",
                table: "Preferreds");

            migrationBuilder.DropColumn(
                name: "RfqId",
                table: "Preferreds");
        }
    }
}

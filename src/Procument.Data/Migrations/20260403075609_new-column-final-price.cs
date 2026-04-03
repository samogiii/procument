using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class newcolumnfinalprice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RFQUserReads_RFQs_RFQId",
                table: "RFQUserReads");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQUserReads_Users_UserId",
                table: "RFQUserReads");

            migrationBuilder.DropIndex(
                name: "IX_RFQUserReads_UserId",
                table: "RFQUserReads");

            migrationBuilder.AddColumn<decimal>(
                name: "FinalPrice",
                table: "Quotes",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalPrice",
                table: "Quotes");

            migrationBuilder.CreateIndex(
                name: "IX_RFQUserReads_UserId",
                table: "RFQUserReads",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RFQUserReads_RFQs_RFQId",
                table: "RFQUserReads",
                column: "RFQId",
                principalTable: "RFQs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQUserReads_Users_UserId",
                table: "RFQUserReads",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

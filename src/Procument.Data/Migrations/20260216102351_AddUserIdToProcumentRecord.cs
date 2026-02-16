using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToProcumentRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Procument",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Procument_UserId",
                table: "Procument",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Procument_Users_UserId",
                table: "Procument",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Procument_Users_UserId",
                table: "Procument");

            migrationBuilder.DropIndex(
                name: "IX_Procument_UserId",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Procument");
        }
    }
}

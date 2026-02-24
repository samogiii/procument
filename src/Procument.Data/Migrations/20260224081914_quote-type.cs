using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class quotetype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeAdditional",
                table: "Quotes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "TypeAdditional",
                table: "Quotes");
        }
    }
}

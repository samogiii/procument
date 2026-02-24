using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class removefleetaddpriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "RFQs");

            migrationBuilder.DropColumn(
                name: "Fleet",
                table: "PartNumbers");

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "RFQItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "RFQItems");

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "RFQs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fleet",
                table: "PartNumbers",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}

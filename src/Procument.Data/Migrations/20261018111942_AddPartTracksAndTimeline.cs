using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartTracksAndTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "Preferreds",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Qty",
                table: "Preferreds",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetPrice",
                table: "Preferreds",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Track",
                table: "Preferreds",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ILS");

            migrationBuilder.AddColumn<long>(
                name: "PreferredPartId",
                table: "PendingSupplierQuotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PreferredEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreferredId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferredEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreferredEvents_Preferreds_PreferredId",
                        column: x => x.PreferredId,
                        principalTable: "Preferreds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingSupplierQuotes_PreferredPartId",
                table: "PendingSupplierQuotes",
                column: "PreferredPartId");

            migrationBuilder.CreateIndex(
                name: "IX_PreferredEvents_PreferredId_CreatedAt",
                table: "PreferredEvents",
                columns: new[] { "PreferredId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreferredEvents");

            migrationBuilder.DropIndex(
                name: "IX_PendingSupplierQuotes_PreferredPartId",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Preferreds");

            migrationBuilder.DropColumn(
                name: "Qty",
                table: "Preferreds");

            migrationBuilder.DropColumn(
                name: "TargetPrice",
                table: "Preferreds");

            migrationBuilder.DropColumn(
                name: "Track",
                table: "Preferreds");

            migrationBuilder.DropColumn(
                name: "PreferredPartId",
                table: "PendingSupplierQuotes");
        }
    }
}

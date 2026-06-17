using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddILSItemSerials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ILSItemSerials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ILSItemId = table.Column<long>(type: "bigint", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeadTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CertImageFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CertImageOriginalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PartImageFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PartImageOriginalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TagDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ILSItemSerials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ILSItemSerials_ILSItems_ILSItemId",
                        column: x => x.ILSItemId,
                        principalTable: "ILSItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ILSItemSerials_ILSItemId",
                table: "ILSItemSerials",
                column: "ILSItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ILSItemSerials");
        }
    }
}

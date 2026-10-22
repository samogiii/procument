using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpCategoryRefsAndWorkStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "CustomerFollowUps",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcurementStatus",
                table: "CustomerFollowUps",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.AddColumn<long>(
                name: "RfqId",
                table: "CustomerFollowUps",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesStatus",
                table: "CustomerFollowUps",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.CreateTable(
                name: "CustomerFollowUpRefs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerFollowUpId = table.Column<long>(type: "bigint", nullable: false),
                    RefType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RefId = table.Column<long>(type: "bigint", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFollowUpRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerFollowUpRefs_CustomerFollowUps_CustomerFollowUpId",
                        column: x => x.CustomerFollowUpId,
                        principalTable: "CustomerFollowUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUpRefs_CustomerFollowUpId_RefType_RefId",
                table: "CustomerFollowUpRefs",
                columns: new[] { "CustomerFollowUpId", "RefType", "RefId" },
                unique: true);

            // Customer Follow Ups procurement users (Sam, 2026-10-20) — editable later in Menu Access.
            migrationBuilder.Sql(@"
INSERT INTO MenuPermissions (Feature, UserName, CreatedAt)
SELECT 'followUpProcurement', u.UserName, SYSUTCDATETIME()
FROM (VALUES ('GHS'), ('AMJ'), ('SHBN'), ('AHM'), ('MOR')) AS u(UserName)
WHERE NOT EXISTS (
    SELECT 1 FROM MenuPermissions m WHERE m.Feature = 'followUpProcurement' AND m.UserName = u.UserName);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM MenuPermissions WHERE Feature = 'followUpProcurement';");

            migrationBuilder.DropTable(
                name: "CustomerFollowUpRefs");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "CustomerFollowUps");

            migrationBuilder.DropColumn(
                name: "ProcurementStatus",
                table: "CustomerFollowUps");

            migrationBuilder.DropColumn(
                name: "RfqId",
                table: "CustomerFollowUps");

            migrationBuilder.DropColumn(
                name: "SalesStatus",
                table: "CustomerFollowUps");
        }
    }
}

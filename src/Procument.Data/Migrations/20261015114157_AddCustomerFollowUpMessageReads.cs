using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFollowUpMessageReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerFollowUpMessageReads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerFollowUpId = table.Column<long>(type: "bigint", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastReadMessageId = table.Column<long>(type: "bigint", nullable: false),
                    MarkedUnread = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFollowUpMessageReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerFollowUpMessageReads_CustomerFollowUps_CustomerFollowUpId",
                        column: x => x.CustomerFollowUpId,
                        principalTable: "CustomerFollowUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUpMessageReads_CustomerFollowUpId_UserName",
                table: "CustomerFollowUpMessageReads",
                columns: new[] { "CustomerFollowUpId", "UserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUpMessageReads_UserName",
                table: "CustomerFollowUpMessageReads",
                column: "UserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerFollowUpMessageReads");
        }
    }
}

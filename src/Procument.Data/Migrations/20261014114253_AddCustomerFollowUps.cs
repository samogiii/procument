using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerFollowUps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedItem = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DateOfRequest = table.Column<DateTime>(type: "date", nullable: true),
                    Deadline = table.Column<DateTime>(type: "date", nullable: true),
                    Assign = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponseStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateOfReply = table.Column<DateTime>(type: "date", nullable: true),
                    OurResponse = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFollowUps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUps_IsActive",
                table: "CustomerFollowUps",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerFollowUps");
        }
    }
}

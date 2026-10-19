using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingRfqItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingRfqItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailSubject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    CustomerNameRaw = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CustomerId = table.Column<long>(type: "bigint", nullable: true),
                    RfqName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeadlineRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PartNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Qty = table.Column<double>(type: "float", nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Alternatives = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RfqId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRfqName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmailId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRfqItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingRfqItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PendingRfqItems_PendingQuoteEmails_EmailId",
                        column: x => x.EmailId,
                        principalTable: "PendingQuoteEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_CustomerEmail",
                table: "PendingRfqItems",
                column: "CustomerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_CustomerId",
                table: "PendingRfqItems",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_EmailId",
                table: "PendingRfqItems",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_MessageId",
                table: "PendingRfqItems",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_RfqId",
                table: "PendingRfqItems",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_Status",
                table: "PendingRfqItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingRfqItems");
        }
    }
}

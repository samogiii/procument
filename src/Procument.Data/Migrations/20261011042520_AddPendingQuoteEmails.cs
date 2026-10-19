using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingQuoteEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EmailId",
                table: "PendingSupplierQuotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingQuoteEmails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FromHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FromEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ToHeader = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CcHeader = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EmailDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingQuoteEmails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingQuoteEmailAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingQuoteEmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingQuoteEmailAttachments_PendingQuoteEmails_EmailId",
                        column: x => x.EmailId,
                        principalTable: "PendingQuoteEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingSupplierQuotes_EmailId",
                table: "PendingSupplierQuotes",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingQuoteEmailAttachments_EmailId",
                table: "PendingQuoteEmailAttachments",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingQuoteEmails_MessageId",
                table: "PendingQuoteEmails",
                column: "MessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_PendingSupplierQuotes_PendingQuoteEmails_EmailId",
                table: "PendingSupplierQuotes",
                column: "EmailId",
                principalTable: "PendingQuoteEmails",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PendingSupplierQuotes_PendingQuoteEmails_EmailId",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropTable(
                name: "PendingQuoteEmailAttachments");

            migrationBuilder.DropTable(
                name: "PendingQuoteEmails");

            migrationBuilder.DropIndex(
                name: "IX_PendingSupplierQuotes_EmailId",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "EmailId",
                table: "PendingSupplierQuotes");
        }
    }
}

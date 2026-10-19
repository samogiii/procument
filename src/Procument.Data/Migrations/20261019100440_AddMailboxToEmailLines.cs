using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxToEmailLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mailbox",
                table: "PendingSupplierQuotes",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailboxLabel",
                table: "PendingSupplierQuotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mailbox",
                table: "PendingRfqItems",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailboxLabel",
                table: "PendingRfqItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingSupplierQuotes_Mailbox",
                table: "PendingSupplierQuotes",
                column: "Mailbox");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_Mailbox",
                table: "PendingRfqItems",
                column: "Mailbox");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingSupplierQuotes_Mailbox",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropIndex(
                name: "IX_PendingRfqItems_Mailbox",
                table: "PendingRfqItems");

            migrationBuilder.DropColumn(
                name: "Mailbox",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "MailboxLabel",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "Mailbox",
                table: "PendingRfqItems");

            migrationBuilder.DropColumn(
                name: "MailboxLabel",
                table: "PendingRfqItems");
        }
    }
}

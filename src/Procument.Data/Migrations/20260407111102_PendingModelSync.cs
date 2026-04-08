using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionCategory",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextData",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityDisplayName",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinalInvoiceItems_InvoiceItemId",
                table: "FinalInvoiceItems",
                column: "InvoiceItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinalInvoiceItems_InvoiceItems_InvoiceItemId",
                table: "FinalInvoiceItems",
                column: "InvoiceItemId",
                principalTable: "InvoiceItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinalInvoiceItems_InvoiceItems_InvoiceItemId",
                table: "FinalInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_FinalInvoiceItems_InvoiceItemId",
                table: "FinalInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ActionCategory",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ContextData",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EntityDisplayName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "AuditLogs");
        }
    }
}

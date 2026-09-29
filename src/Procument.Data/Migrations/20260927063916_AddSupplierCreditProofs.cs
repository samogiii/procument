using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <summary>
    /// Supplier Credits E9 — proof of where a credit came from: origin PO (or text), the supplier's
    /// credit memo, and any number of documents. Hand-edited from the scaffold so no data is lost:
    /// the old "Reference" (supplier credit-note number) becomes CreditMemo, and the old single
    /// DocumentFileName moves into SupplierCreditDocuments before the column is dropped.
    /// </summary>
    public partial class AddSupplierCreditProofs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierCreditDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditDocuments", x => x.Id);
                    table.CheckConstraint("CK_SupplierCreditDocuments_Category", "[Category] IN ('SupplierPI','CreditMemo','Other')");
                    table.ForeignKey(
                        name: "FK_SupplierCreditDocuments_SupplierCreditEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "SupplierCreditEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierCreditDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            // Keep any credit-note file attached before E9. Stored names are "{requestId:N}-{original}".
            migrationBuilder.Sql("""
                INSERT INTO [SupplierCreditDocuments] ([EntryId], [Category], [FileName], [OriginalFileName], [SizeBytes], [UploadedAt], [UploadedByUserId])
                SELECT [Id], 'CreditMemo', [DocumentFileName],
                       CASE WHEN LEN([DocumentFileName]) > 33 AND SUBSTRING([DocumentFileName], 33, 1) = '-'
                            THEN SUBSTRING([DocumentFileName], 34, 260) ELSE [DocumentFileName] END,
                       0, [CreatedAt], [CreatedByUserId]
                FROM [SupplierCreditEntries]
                WHERE [DocumentFileName] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "DocumentFileName",
                table: "SupplierCreditEntries");

            // The supplier's credit-note number is the credit memo; widen it for free text.
            migrationBuilder.RenameColumn(
                name: "Reference",
                table: "SupplierCreditEntries",
                newName: "CreditMemo");

            migrationBuilder.AlterColumn<string>(
                name: "CreditMemo",
                table: "SupplierCreditEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePoReference",
                table: "SupplierCreditEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourcePurchaseOrderId",
                table: "SupplierCreditEntries",
                type: "bigint",
                nullable: true);

            // Credit notes recorded before origins were asked for.
            migrationBuilder.Sql("""
                UPDATE [SupplierCreditEntries]
                SET [SourcePoReference] = N'(not recorded)'
                WHERE [Source] = 'CreditNote' AND [SourcePurchaseOrderId] IS NULL AND [SourcePoReference] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditEntries_SourcePurchaseOrderId",
                table: "SupplierCreditEntries",
                column: "SourcePurchaseOrderId",
                filter: "[SourcePurchaseOrderId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierCreditEntries_SourcePo",
                table: "SupplierCreditEntries",
                sql: "[SourcePurchaseOrderId] IS NULL OR [SourcePoReference] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditDocuments_EntryId",
                table: "SupplierCreditDocuments",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditDocuments_UploadedByUserId",
                table: "SupplierCreditDocuments",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierCreditEntries_PurchaseOrders_SourcePurchaseOrderId",
                table: "SupplierCreditEntries",
                column: "SourcePurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierCreditEntries_PurchaseOrders_SourcePurchaseOrderId",
                table: "SupplierCreditEntries");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCreditEntries_SourcePurchaseOrderId",
                table: "SupplierCreditEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierCreditEntries_SourcePo",
                table: "SupplierCreditEntries");

            migrationBuilder.DropColumn(
                name: "SourcePurchaseOrderId",
                table: "SupplierCreditEntries");

            migrationBuilder.DropColumn(
                name: "SourcePoReference",
                table: "SupplierCreditEntries");

            migrationBuilder.AlterColumn<string>(
                name: "CreditMemo",
                table: "SupplierCreditEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "CreditMemo",
                table: "SupplierCreditEntries",
                newName: "Reference");

            migrationBuilder.AddColumn<string>(
                name: "DocumentFileName",
                table: "SupplierCreditEntries",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            // Best effort: the first document of each entry goes back into the single-file column.
            migrationBuilder.Sql("""
                UPDATE e SET [DocumentFileName] = d.[FileName]
                FROM [SupplierCreditEntries] e
                CROSS APPLY (SELECT TOP 1 [FileName] FROM [SupplierCreditDocuments] WHERE [EntryId] = e.[Id] ORDER BY [Id]) d;
                """);

            migrationBuilder.DropTable(
                name: "SupplierCreditDocuments");
        }
    }
}

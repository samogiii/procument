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
            // Add columns with IF NOT EXISTS checks
            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'ActionCategory') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [ActionCategory] nvarchar(max) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'ContextData') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [ContextData] nvarchar(max) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'EntityDisplayName') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [EntityDisplayName] nvarchar(max) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'RelatedEntityId') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [RelatedEntityId] nvarchar(max) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'RelatedEntityType') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [RelatedEntityType] nvarchar(max) NULL;
                END");

            // Create index with IF NOT EXISTS check
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FinalInvoiceItems_InvoiceItemId' AND object_id = OBJECT_ID('FinalInvoiceItems'))
                BEGIN
                    CREATE INDEX [IX_FinalInvoiceItems_InvoiceItemId] ON [FinalInvoiceItems] ([InvoiceItemId]);
                END");

            // Add foreign key with IF NOT EXISTS check
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FinalInvoiceItems_InvoiceItems_InvoiceItemId' AND parent_object_id = OBJECT_ID('FinalInvoiceItems'))
                BEGIN
                    ALTER TABLE [FinalInvoiceItems] ADD CONSTRAINT [FK_FinalInvoiceItems_InvoiceItems_InvoiceItemId]
                    FOREIGN KEY ([InvoiceItemId]) REFERENCES [InvoiceItems] ([Id]);
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key with IF EXISTS check
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FinalInvoiceItems_InvoiceItems_InvoiceItemId' AND parent_object_id = OBJECT_ID('FinalInvoiceItems'))
                BEGIN
                    ALTER TABLE [FinalInvoiceItems] DROP CONSTRAINT [FK_FinalInvoiceItems_InvoiceItems_InvoiceItemId];
                END");

            // Drop index with IF EXISTS check
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FinalInvoiceItems_InvoiceItemId' AND object_id = OBJECT_ID('FinalInvoiceItems'))
                BEGIN
                    DROP INDEX [IX_FinalInvoiceItems_InvoiceItemId] ON [FinalInvoiceItems];
                END");

            // Drop columns with IF EXISTS checks
            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'RelatedEntityType') IS NOT NULL
                BEGIN
                    ALTER TABLE [AuditLogs] DROP COLUMN [RelatedEntityType];
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'RelatedEntityId') IS NOT NULL
                BEGIN
                    ALTER TABLE [AuditLogs] DROP COLUMN [RelatedEntityId];
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'EntityDisplayName') IS NOT NULL
                BEGIN
                    ALTER TABLE [AuditLogs] DROP COLUMN [EntityDisplayName];
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'ContextData') IS NOT NULL
                BEGIN
                    ALTER TABLE [AuditLogs] DROP COLUMN [ContextData];
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'ActionCategory') IS NOT NULL
                BEGIN
                    ALTER TABLE [AuditLogs] DROP COLUMN [ActionCategory];
                END");
        }
    }
}

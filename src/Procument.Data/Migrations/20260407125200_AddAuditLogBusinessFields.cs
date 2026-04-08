using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogBusinessFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns with IF NOT EXISTS check
            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'ContextData') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [ContextData] nvarchar(max) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'ActionCategory') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [ActionCategory] nvarchar(50) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'EntityDisplayName') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [EntityDisplayName] nvarchar(200) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'RelatedEntityId') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [RelatedEntityId] nvarchar(50) NULL;
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('AuditLogs', 'RelatedEntityType') IS NULL
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [RelatedEntityType] nvarchar(50) NULL;
                END");

            // Migrate existing data - populate new fields based on existing data
            migrationBuilder.Sql(@"
                UPDATE AuditLogs
                SET ActionCategory = 
                    CASE 
                        WHEN Action LIKE '%create%' OR Action LIKE '%Create%' OR Action LIKE '%add%' OR Action LIKE '%Add%' THEN 'Creation'
                        WHEN Action LIKE '%update%' OR Action LIKE '%Update%' OR Action LIKE '%edit%' OR Action LIKE '%Edit%' THEN 'Update'
                        WHEN Action LIKE '%delete%' OR Action LIKE '%Delete%' OR Action LIKE '%remove%' OR Action LIKE '%Remove%' THEN 'Deletion'
                        WHEN Action LIKE '%status%' OR Action LIKE '%Status%' THEN 'StatusChange'
                        ELSE 'Update'
                    END
                WHERE ActionCategory IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE AuditLogs
                SET EntityDisplayName = EntityName + ' #' + EntityId
                WHERE EntityDisplayName IS NULL AND EntityId IS NOT NULL;
            ");

            // For logs with no EntityId, just use the EntityName
            migrationBuilder.Sql(@"
                UPDATE AuditLogs
                SET EntityDisplayName = EntityName
                WHERE EntityDisplayName IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RelatedEntityType", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "RelatedEntityId", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "EntityDisplayName", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "ActionCategory", table: "AuditLogs");
            migrationBuilder.DropColumn(name: "ContextData", table: "AuditLogs");
        }
    }
}

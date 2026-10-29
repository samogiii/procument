using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPpeAndPerUserPreferreds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PpeAssignedAt",
                table: "PendingRfqItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PpeAssignedByUserId",
                table: "PendingRfqItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PpeUserId",
                table: "PendingRfqItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRfqItems_PpeUserId",
                table: "PendingRfqItems",
                column: "PpeUserId");

            // The old Procurement Plus is now Procurement ProMax (own feature, never in the menu): its users keep it,
            // and keep "procurementPlus", which is now the new page (Sam, 2026-10-25).
            migrationBuilder.Sql(@"
INSERT INTO MenuPermissions (Feature, UserName, CreatedAt)
SELECT 'procurementProMax', m.UserName, SYSUTCDATETIME()
FROM MenuPermissions m
WHERE m.Feature = 'procurementPlus'
  AND NOT EXISTS (SELECT 1 FROM MenuPermissions x WHERE x.Feature = 'procurementProMax' AND x.UserName = m.UserName);");

            // Preferred suppliers become per user (Sam: copy the shared list to every user): each user with either
            // page, and every SuperAdmin, gets their own copy; lines being sourced point at their user's copy.
            migrationBuilder.Sql(@"
INSERT INTO Preferreds (Kind, UserId, Name, NameKey, Description, Alternatives, Emails, Phone, SourceMethod, Note,
                        PartNumberId, SupplierId, AddedAt, AddedByUserId, HiddenAt, Track, Qty, Condition, TargetPrice, RfqId)
SELECT p.Kind, u.Id, p.Name, p.NameKey, p.Description, p.Alternatives, p.Emails, p.Phone, p.SourceMethod, p.Note,
       p.PartNumberId, p.SupplierId, p.AddedAt, p.AddedByUserId, p.HiddenAt, p.Track, p.Qty, p.Condition, p.TargetPrice, p.RfqId
FROM Preferreds p
CROSS JOIN (
    SELECT DISTINCT us.Id FROM Users us
    WHERE us.Role = 'SuperAdmin'
       OR EXISTS (SELECT 1 FROM MenuPermissions m
                  WHERE m.UserName = us.Name AND m.Feature IN ('procurementPlus', 'procurementProMax'))
) u
WHERE p.Kind = 'Supplier' AND p.UserId IS NULL;

UPDATE q SET q.PreferredId = (
    SELECT MIN(n.Id) FROM Preferreds n
    WHERE n.Kind = 'Supplier' AND n.UserId = q.CreatedByUserId AND n.NameKey = o.NameKey)
FROM PendingSupplierQuotes q
JOIN Preferreds o ON o.Id = q.PreferredId AND o.Kind = 'Supplier' AND o.UserId IS NULL;

DELETE FROM Preferreds WHERE Kind = 'Supplier' AND UserId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Per-user preferred suppliers are kept (no way to tell which copy was the shared one).
            migrationBuilder.Sql("DELETE FROM MenuPermissions WHERE Feature = 'procurementProMax';");

            migrationBuilder.DropIndex(
                name: "IX_PendingRfqItems_PpeUserId",
                table: "PendingRfqItems");

            migrationBuilder.DropColumn(
                name: "PpeAssignedAt",
                table: "PendingRfqItems");

            migrationBuilder.DropColumn(
                name: "PpeAssignedByUserId",
                table: "PendingRfqItems");

            migrationBuilder.DropColumn(
                name: "PpeUserId",
                table: "PendingRfqItems");
        }
    }
}

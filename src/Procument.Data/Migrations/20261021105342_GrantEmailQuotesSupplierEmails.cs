using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class GrantEmailQuotesSupplierEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Email Quotes: only AMJ's and GHS's accept adds the sender's email to the supplier (Sam, 2026-10-21) — editable in Menu Access.
            migrationBuilder.Sql(@"
INSERT INTO MenuPermissions (Feature, UserName, CreatedAt)
SELECT 'emailQuotesSupplierEmails', u.UserName, SYSUTCDATETIME()
FROM (VALUES ('AMJ'), ('GHS')) AS u(UserName)
WHERE NOT EXISTS (
    SELECT 1 FROM MenuPermissions m WHERE m.Feature = 'emailQuotesSupplierEmails' AND m.UserName = u.UserName);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM MenuPermissions WHERE Feature = 'emailQuotesSupplierEmails';");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <summary>
    /// The quote email dialog used to be limited to a hard-coded list of users (AMJ, SHBN, MGH,
    /// System Admin). It is now the "sendQuoteEmail" Menu Access feature; grant it to the same
    /// people so nobody loses access. System Admin is SuperAdmin and always passes.
    /// </summary>
    public partial class GrantSendQuoteEmailPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO MenuPermissions (Feature, UserName, CreatedAt)
SELECT 'sendQuoteEmail', u.UserName, SYSUTCDATETIME()
FROM (VALUES ('AMJ'), ('SHBN'), ('MGH')) AS u(UserName)
WHERE NOT EXISTS (
    SELECT 1 FROM MenuPermissions m WHERE m.Feature = 'sendQuoteEmail' AND m.UserName = u.UserName);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM MenuPermissions WHERE Feature = 'sendQuoteEmail';");
        }
    }
}

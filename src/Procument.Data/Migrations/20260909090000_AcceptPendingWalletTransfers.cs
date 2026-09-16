using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Procument.Data;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260909090000_AcceptPendingWalletTransfers")]
    public partial class AcceptPendingWalletTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE WalletTransferPendings
                SET Status = 'Accepted',
                    AcceptedByUserId = COALESCE(AcceptedByUserId, CreatedByUserId),
                    AcceptedAt = COALESCE(AcceptedAt, CreatedAt)
                WHERE Status = 'Pending';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Existing transfers cannot be reliably distinguished from transfers
            // created after this workflow change, so this data migration is one-way.
        }
    }
}

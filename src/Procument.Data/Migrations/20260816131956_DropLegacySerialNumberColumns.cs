using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <summary>
    /// The first cut of the Base 1 numbering shipped these columns as "SerialNumber" and was
    /// applied to the database before they were renamed to B1QuoteNumber / B1InvoiceNumber /
    /// B1FinalInvoiceNumber. The migration that created them has since been removed from the
    /// project, so this one carries any values across — losing them would restart each
    /// customer's monthly sequence at 10 — and then drops the orphaned columns.
    ///
    /// Written by hand: it makes no model changes, so EF has nothing to scaffold. Every
    /// statement is guarded, so it is a no-op on a database that never saw the old columns.
    /// </summary>
    public partial class DropLegacySerialNumberColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Carry values across, without overwriting anything already set ──
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Quotes', 'SerialNumber') IS NOT NULL
                    EXEC('UPDATE Quotes SET B1QuoteNumber = SerialNumber
                          WHERE B1QuoteNumber IS NULL AND SerialNumber IS NOT NULL');");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('Invoices', 'SerialNumber') IS NOT NULL
                    EXEC('UPDATE Invoices SET B1InvoiceNumber = SerialNumber
                          WHERE B1InvoiceNumber IS NULL AND SerialNumber IS NOT NULL');");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('FinalInvoices', 'SerialNumber') IS NOT NULL
                    EXEC('UPDATE FinalInvoices SET B1FinalInvoiceNumber = SerialNumber
                          WHERE B1FinalInvoiceNumber IS NULL AND SerialNumber IS NOT NULL');");

            // ── 2. Drop the old indexes, then the columns ──
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Quotes_SerialNumber' AND object_id = OBJECT_ID('Quotes'))
                    DROP INDEX IX_Quotes_SerialNumber ON Quotes;
                IF COL_LENGTH('Quotes', 'SerialNumber') IS NOT NULL
                    ALTER TABLE Quotes DROP COLUMN SerialNumber;");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_SerialNumber' AND object_id = OBJECT_ID('Invoices'))
                    DROP INDEX IX_Invoices_SerialNumber ON Invoices;
                IF COL_LENGTH('Invoices', 'SerialNumber') IS NOT NULL
                    ALTER TABLE Invoices DROP COLUMN SerialNumber;");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FinalInvoices_SerialNumber' AND object_id = OBJECT_ID('FinalInvoices'))
                    DROP INDEX IX_FinalInvoices_SerialNumber ON FinalInvoices;
                IF COL_LENGTH('FinalInvoices', 'SerialNumber') IS NOT NULL
                    ALTER TABLE FinalInvoices DROP COLUMN SerialNumber;");

            // ── 3. Forget the removed migration, so history matches the Migrations folder ──
            migrationBuilder.Sql(
                "DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20260816112004_AddDocumentSerialNumbers';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. The legacy columns were replaced by the B1* ones, which the
            // AddB1DocumentNumbers migration owns; recreating them here would resurrect a
            // schema no code reads.
        }
    }
}

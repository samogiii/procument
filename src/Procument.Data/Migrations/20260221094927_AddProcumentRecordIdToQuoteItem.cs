using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcumentRecordIdToQuoteItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProcumentRecordId",
                table: "QuoteItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteItems_ProcumentRecordId",
                table: "QuoteItems",
                column: "ProcumentRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteItems_Procument_ProcumentRecordId",
                table: "QuoteItems",
                column: "ProcumentRecordId",
                principalTable: "Procument",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteItems_Procument_ProcumentRecordId",
                table: "QuoteItems");

            migrationBuilder.DropIndex(
                name: "IX_QuoteItems_ProcumentRecordId",
                table: "QuoteItems");

            migrationBuilder.DropColumn(
                name: "ProcumentRecordId",
                table: "QuoteItems");
        }
    }
}

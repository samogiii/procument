using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPresetToSupplierPartQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompanyPresetId",
                table: "SupplierPartQuote",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPartQuote_CompanyPresetId",
                table: "SupplierPartQuote",
                column: "CompanyPresetId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPartQuote_CompanyPresets_CompanyPresetId",
                table: "SupplierPartQuote",
                column: "CompanyPresetId",
                principalTable: "CompanyPresets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPartQuote_CompanyPresets_CompanyPresetId",
                table: "SupplierPartQuote");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPartQuote_CompanyPresetId",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "CompanyPresetId",
                table: "SupplierPartQuote");
        }
    }
}

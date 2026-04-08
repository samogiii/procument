using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPresetColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('CompanyPresets', 'PrimaryColor') IS NULL
                BEGIN
                    ALTER TABLE [CompanyPresets] ADD [PrimaryColor] nvarchar(20) NOT NULL DEFAULT N'#1a2744';
                END");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('CompanyPresets', 'AccentColor') IS NULL
                BEGIN
                    ALTER TABLE [CompanyPresets] ADD [AccentColor] nvarchar(20) NOT NULL DEFAULT N'#2563eb';
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PrimaryColor", table: "CompanyPresets");
            migrationBuilder.DropColumn(name: "AccentColor", table: "CompanyPresets");
        }
    }
}

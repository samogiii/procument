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
            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "CompanyPresets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "#1a2744");

            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "CompanyPresets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "#2563eb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PrimaryColor", table: "CompanyPresets");
            migrationBuilder.DropColumn(name: "AccentColor", table: "CompanyPresets");
        }
    }
}

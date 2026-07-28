using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImapSettingsToCompanyPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImapEnabled",
                table: "CompanyPresets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ImapHost",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImapPort",
                table: "CompanyPresets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImapSentFolder",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImapUseSsl",
                table: "CompanyPresets",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapEnabled",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "ImapHost",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "ImapPort",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "ImapSentFolder",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "ImapUseSsl",
                table: "CompanyPresets");
        }
    }
}

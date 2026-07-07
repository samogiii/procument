using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmtpSettingsToCompanyPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SmtpEnabled",
                table: "CompanyPresets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromDisplayName",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromEmail",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPasswordEncrypted",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPasswordIv",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "CompanyPresets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "CompanyPresets",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUsername",
                table: "CompanyPresets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmtpEnabled",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpFromDisplayName",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpFromEmail",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpPasswordEncrypted",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpPasswordIv",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "CompanyPresets");

            migrationBuilder.DropColumn(
                name: "SmtpUsername",
                table: "CompanyPresets");
        }
    }
}

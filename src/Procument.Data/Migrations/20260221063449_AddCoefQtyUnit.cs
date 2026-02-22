using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoefQtyUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "Qty",
                table: "RFQItems",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "RFQItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Qty",
                table: "Procument",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CertName",
                table: "Procument",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Coef_1",
                table: "Procument",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Coef_2",
                table: "Procument",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Coef_3",
                table: "Procument",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadTime",
                table: "Procument",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ShippingCost",
                table: "Procument",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingPoint",
                table: "Procument",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TagDate",
                table: "Procument",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Procument",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "RFQItems");

            migrationBuilder.DropColumn(
                name: "CertName",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "Coef_1",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "Coef_2",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "Coef_3",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "LeadTime",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "ShippingPoint",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "TagDate",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Procument");

            migrationBuilder.AlterColumn<int>(
                name: "Qty",
                table: "RFQItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<int>(
                name: "Qty",
                table: "Procument",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");
        }
    }
}

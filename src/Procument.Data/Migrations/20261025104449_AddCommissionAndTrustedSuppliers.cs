using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionAndTrustedSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Trusted",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommissionAt",
                table: "SupplierPartQuote",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CommissionByUserId",
                table: "SupplierPartQuote",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinCoef",
                table: "Customers",
                type: "decimal(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Trusted",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CommissionAt",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "CommissionByUserId",
                table: "SupplierPartQuote");

            migrationBuilder.DropColumn(
                name: "MinCoef",
                table: "Customers");
        }
    }
}

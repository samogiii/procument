using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompanyPresetId",
                table: "PaymentRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentBoxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyPresetId = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentBoxes_CompanyPresets_CompanyPresetId",
                        column: x => x.CompanyPresetId,
                        principalTable: "CompanyPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentBoxId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FromType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    ToType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToSupplierId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentRequestId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAuto = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Customers_FromCustomerId",
                        column: x => x.FromCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentBoxes_PaymentBoxId",
                        column: x => x.PaymentBoxId,
                        principalTable: "PaymentBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentRequests_PaymentRequestId",
                        column: x => x.PaymentRequestId,
                        principalTable: "PaymentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Suppliers_ToSupplierId",
                        column: x => x.ToSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_CompanyPresetId",
                table: "PaymentRequests",
                column: "CompanyPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBoxes_CompanyPresetId",
                table: "PaymentBoxes",
                column: "CompanyPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_CreatedAt",
                table: "PaymentTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_FromCustomerId",
                table: "PaymentTransactions",
                column: "FromCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InvoiceId",
                table: "PaymentTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentBoxId",
                table: "PaymentTransactions",
                column: "PaymentBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentRequestId",
                table: "PaymentTransactions",
                column: "PaymentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ToSupplierId",
                table: "PaymentTransactions",
                column: "ToSupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_CompanyPresets_CompanyPresetId",
                table: "PaymentRequests",
                column: "CompanyPresetId",
                principalTable: "CompanyPresets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_CompanyPresets_CompanyPresetId",
                table: "PaymentRequests");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "PaymentBoxes");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_CompanyPresetId",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "CompanyPresetId",
                table: "PaymentRequests");
        }
    }
}

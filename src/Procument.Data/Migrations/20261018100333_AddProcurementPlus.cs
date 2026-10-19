using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementPlus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedByUserId",
                table: "PendingSupplierQuotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "PendingSupplierQuotes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMethod",
                table: "PendingSupplierQuotes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PreferredSuppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    SourceMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferredSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreferredSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPlusParts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: true),
                    PartNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HiddenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlusParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPlusParts_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPlusUploads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlusUploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierAliases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AliasKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierAliases_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementPlusOffers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<long>(type: "bigint", nullable: false),
                    PartNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AltPartNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AltKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyRaw = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CompanyKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QtyRaw = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Qty = table.Column<double>(type: "float", nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Distance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Cage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Exch = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlusOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPlusOffers_ProcurementPlusUploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "ProcurementPlusUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementPlusOffers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingSupplierQuotes_Source_Status",
                table: "PendingSupplierQuotes",
                columns: new[] { "Source", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PreferredSuppliers_SupplierId",
                table: "PreferredSuppliers",
                column: "SupplierId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusOffers_AltKey",
                table: "ProcurementPlusOffers",
                column: "AltKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusOffers_CompanyKey",
                table: "ProcurementPlusOffers",
                column: "CompanyKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusOffers_PartKey",
                table: "ProcurementPlusOffers",
                column: "PartKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusOffers_SupplierId",
                table: "ProcurementPlusOffers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusOffers_UploadId",
                table: "ProcurementPlusOffers",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusParts_PartNumberId",
                table: "ProcurementPlusParts",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusParts_UserId_PartKey",
                table: "ProcurementPlusParts",
                columns: new[] { "UserId", "PartKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusUploads_UserId_UploadedAt",
                table: "ProcurementPlusUploads",
                columns: new[] { "UserId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAliases_AliasKey",
                table: "SupplierAliases",
                column: "AliasKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAliases_SupplierId",
                table: "SupplierAliases",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreferredSuppliers");

            migrationBuilder.DropTable(
                name: "ProcurementPlusOffers");

            migrationBuilder.DropTable(
                name: "ProcurementPlusParts");

            migrationBuilder.DropTable(
                name: "SupplierAliases");

            migrationBuilder.DropTable(
                name: "ProcurementPlusUploads");

            migrationBuilder.DropIndex(
                name: "IX_PendingSupplierQuotes_Source_Status",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "SourceMethod",
                table: "PendingSupplierQuotes");
        }
    }
}

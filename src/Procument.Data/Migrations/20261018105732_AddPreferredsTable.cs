using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PreferredId",
                table: "PendingSupplierQuotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Preferreds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NameKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Alternatives = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Emails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    HiddenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preferreds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Preferreds_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Preferreds_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingSupplierQuotes_PreferredId",
                table: "PendingSupplierQuotes",
                column: "PreferredId");

            migrationBuilder.CreateIndex(
                name: "IX_Preferreds_Kind_UserId_NameKey",
                table: "Preferreds",
                columns: new[] { "Kind", "UserId", "NameKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Preferreds_PartNumberId",
                table: "Preferreds",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_Preferreds_SupplierId",
                table: "Preferreds",
                column: "SupplierId");

            // Existing preferred parts / suppliers move into Preferreds (they keep their own name, emails…).
            migrationBuilder.Sql(@"
INSERT INTO Preferreds (Kind, UserId, Name, NameKey, Description, PartNumberId, AddedAt, AddedByUserId, HiddenAt)
SELECT 'Part', p.UserId, p.PartNumber, p.PartKey, p.Description, p.PartNumberId, p.AddedAt, p.UserId, p.HiddenAt
FROM ProcurementPlusParts p;

INSERT INTO Preferreds (Kind, UserId, Name, NameKey, Emails, Phone, SourceMethod, Note, SupplierId, AddedAt, AddedByUserId)
SELECT 'Supplier', NULL, s.Name, UPPER(LTRIM(RTRIM(s.Name))), s.Email, s.Phone, ps.SourceMethod, ps.Note, ps.SupplierId, ps.AddedAt, ps.AddedByUserId
FROM PreferredSuppliers ps JOIN Suppliers s ON s.Id = ps.SupplierId;

UPDATE q SET q.PreferredId = pr.Id
FROM PendingSupplierQuotes q
JOIN Preferreds pr ON pr.Kind = 'Supplier' AND pr.SupplierId = q.SupplierId
WHERE q.Source = 'ProcurementPlus';
");

            migrationBuilder.DropTable(
                name: "PreferredSuppliers");

            migrationBuilder.DropTable(
                name: "ProcurementPlusParts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Preferreds");

            migrationBuilder.DropIndex(
                name: "IX_PendingSupplierQuotes_PreferredId",
                table: "PendingSupplierQuotes");

            migrationBuilder.DropColumn(
                name: "PreferredId",
                table: "PendingSupplierQuotes");

            migrationBuilder.CreateTable(
                name: "PreferredSuppliers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<long>(type: "bigint", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
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
                    PartNumberId = table.Column<long>(type: "bigint", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HiddenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PartKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_PreferredSuppliers_SupplierId",
                table: "PreferredSuppliers",
                column: "SupplierId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusParts_PartNumberId",
                table: "ProcurementPlusParts",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlusParts_UserId_PartKey",
                table: "ProcurementPlusParts",
                columns: new[] { "UserId", "PartKey" },
                unique: true);
        }
    }
}

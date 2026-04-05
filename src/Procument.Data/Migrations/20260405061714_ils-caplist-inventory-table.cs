using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class ilscaplistinventorytable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: add columns only if they don't already exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Procument' AND COLUMN_NAME='FixPrice')
                    ALTER TABLE [Procument] ADD [FixPrice] decimal(18,2) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Procument' AND COLUMN_NAME='ParentProcumentId')
                    ALTER TABLE [Procument] ADD [ParentProcumentId] bigint NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Procument' AND COLUMN_NAME='Type')
                    ALTER TABLE [Procument] ADD [Type] nvarchar(50) NOT NULL DEFAULT 'Procument';
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Procument_ParentProcumentId' AND object_id=OBJECT_ID('Procument'))
                    CREATE INDEX [IX_Procument_ParentProcumentId] ON [Procument]([ParentProcumentId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Procument_Type' AND object_id=OBJECT_ID('Procument'))
                    CREATE INDEX [IX_Procument_Type] ON [Procument]([Type]);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Procument_Procument_ParentProcumentId')
                    ALTER TABLE [Procument] ADD CONSTRAINT [FK_Procument_Procument_ParentProcumentId]
                        FOREIGN KEY ([ParentProcumentId]) REFERENCES [Procument]([Id]) ON DELETE NO ACTION;
            ");

            // Idempotent: create tables only if they don't exist
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[CapListItems]') IS NULL
                BEGIN
                    CREATE TABLE [CapListItems] (
                        [Id] bigint NOT NULL IDENTITY(1,1),
                        [PartNumberId] bigint NOT NULL,
                        [Description] nvarchar(1000) NULL,
                        [CompanyId] bigint NOT NULL,
                        [IsRepair] bit NOT NULL DEFAULT 0,
                        [ProcumentRecordId] bigint NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_CapListItems] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CapListItems_PartNumbers_PartNumberId] FOREIGN KEY ([PartNumberId]) REFERENCES [PartNumbers]([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_CapListItems_Suppliers_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Suppliers]([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_CapListItems_CompanyId] ON [CapListItems]([CompanyId]);
                    CREATE INDEX [IX_CapListItems_CreatedAt] ON [CapListItems]([CreatedAt]);
                    CREATE INDEX [IX_CapListItems_IsRepair] ON [CapListItems]([IsRepair]);
                    CREATE INDEX [IX_CapListItems_PartNumberId] ON [CapListItems]([PartNumberId]);
                    CREATE INDEX [IX_CapListItems_ProcumentRecordId] ON [CapListItems]([ProcumentRecordId]);
                END
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[ILSItems]') IS NULL
                BEGIN
                    CREATE TABLE [ILSItems] (
                        [Id] bigint NOT NULL IDENTITY(1,1),
                        [PartNumberId] bigint NOT NULL,
                        [Description] nvarchar(1000) NULL,
                        [AltPartNumber] nvarchar(200) NULL,
                        [Price] decimal(18,2) NOT NULL DEFAULT 0,
                        [Qty] float NOT NULL DEFAULT 0,
                        [Condition] nvarchar(100) NULL,
                        [TagDate] date NULL,
                        [CertName] nvarchar(200) NULL,
                        [LeadTime] nvarchar(100) NULL,
                        [ProcumentRecordId] bigint NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_ILSItems] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ILSItems_PartNumbers_PartNumberId] FOREIGN KEY ([PartNumberId]) REFERENCES [PartNumbers]([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_ILSItems_CreatedAt] ON [ILSItems]([CreatedAt]);
                    CREATE INDEX [IX_ILSItems_PartNumberId] ON [ILSItems]([PartNumberId]);
                END
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[InventoryItems]') IS NULL
                BEGIN
                    CREATE TABLE [InventoryItems] (
                        [Id] bigint NOT NULL IDENTITY(1,1),
                        [PartNumberId] bigint NOT NULL,
                        [Description] nvarchar(1000) NULL,
                        [Qty] float NOT NULL DEFAULT 0,
                        [CompanyId] bigint NOT NULL,
                        [Condition] nvarchar(100) NULL,
                        [Price] decimal(18,2) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_InventoryItems] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_InventoryItems_PartNumbers_PartNumberId] FOREIGN KEY ([PartNumberId]) REFERENCES [PartNumbers]([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_InventoryItems_Suppliers_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Suppliers]([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_InventoryItems_CompanyId] ON [InventoryItems]([CompanyId]);
                    CREATE INDEX [IX_InventoryItems_CreatedAt] ON [InventoryItems]([CreatedAt]);
                    CREATE INDEX [IX_InventoryItems_PartNumberId] ON [InventoryItems]([PartNumberId]);
                END
            ");

            // Add FK for CapListItems ProcumentRecordId after Procument table confirmed
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_CapListItems_Procument_ProcumentRecordId')
                    ALTER TABLE [CapListItems] ADD CONSTRAINT [FK_CapListItems_Procument_ProcumentRecordId]
                        FOREIGN KEY ([ProcumentRecordId]) REFERENCES [Procument]([Id]) ON DELETE NO ACTION;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_ILSItems_Procument_ProcumentRecordId')
                    ALTER TABLE [ILSItems] ADD CONSTRAINT [FK_ILSItems_Procument_ProcumentRecordId]
                        FOREIGN KEY ([ProcumentRecordId]) REFERENCES [Procument]([Id]) ON DELETE NO ACTION;
            ");
        }

        // dummy block to remove the original CreateTable calls (replaced by SQL above)
        private void _unused(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapListItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    IsRepair = table.Column<bool>(type: "bit", nullable: false),
                    ProcumentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapListItems_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapListItems_Procument_ProcumentRecordId",
                        column: x => x.ProcumentRecordId,
                        principalTable: "Procument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CapListItems_Suppliers_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ILSItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AltPartNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Qty = table.Column<double>(type: "float", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TagDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeadTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcumentRecordId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ILSItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ILSItems_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ILSItems_Procument_ProcumentRecordId",
                        column: x => x.ProcumentRecordId,
                        principalTable: "Procument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartNumberId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Qty = table.Column<double>(type: "float", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Suppliers_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Procument_ParentProcumentId",
                table: "Procument",
                column: "ParentProcumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Procument_Type",
                table: "Procument",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_CapListItems_CompanyId",
                table: "CapListItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CapListItems_CreatedAt",
                table: "CapListItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CapListItems_IsRepair",
                table: "CapListItems",
                column: "IsRepair");

            migrationBuilder.CreateIndex(
                name: "IX_CapListItems_PartNumberId",
                table: "CapListItems",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_CapListItems_ProcumentRecordId",
                table: "CapListItems",
                column: "ProcumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ILSItems_CreatedAt",
                table: "ILSItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ILSItems_PartNumberId",
                table: "ILSItems",
                column: "PartNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_ILSItems_ProcumentRecordId",
                table: "ILSItems",
                column: "ProcumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CompanyId",
                table: "InventoryItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CreatedAt",
                table: "InventoryItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PartNumberId",
                table: "InventoryItems",
                column: "PartNumberId");

            migrationBuilder.AddForeignKey(
                name: "FK_Procument_Procument_ParentProcumentId",
                table: "Procument",
                column: "ParentProcumentId",
                principalTable: "Procument",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Procument_Procument_ParentProcumentId",
                table: "Procument");

            migrationBuilder.DropTable(
                name: "CapListItems");

            migrationBuilder.DropTable(
                name: "ILSItems");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Procument_ParentProcumentId",
                table: "Procument");

            migrationBuilder.DropIndex(
                name: "IX_Procument_Type",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "FixPrice",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "ParentProcumentId",
                table: "Procument");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Procument");
        }
    }
}

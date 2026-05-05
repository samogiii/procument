using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSatelliteSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SatelliteNodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseNumber = table.Column<int>(type: "int", nullable: false),
                    EndpointUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SharedSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatelliteNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRegistries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MainAppId = table.Column<long>(type: "bigint", nullable: false),
                    SatelliteAppId = table.Column<long>(type: "bigint", nullable: false),
                    SatelliteNodeId = table.Column<long>(type: "bigint", nullable: false),
                    LastSyncHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRegistries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncRegistries_SatelliteNodes_SatelliteNodeId",
                        column: x => x.SatelliteNodeId,
                        principalTable: "SatelliteNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatelliteNodes_BaseNumber",
                table: "SatelliteNodes",
                column: "BaseNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRegistries_EntityName_MainAppId",
                table: "SyncRegistries",
                columns: new[] { "EntityName", "MainAppId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncRegistries_EntityName_SatelliteAppId_SatelliteNodeId",
                table: "SyncRegistries",
                columns: new[] { "EntityName", "SatelliteAppId", "SatelliteNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncRegistries_SatelliteNodeId",
                table: "SyncRegistries",
                column: "SatelliteNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncRegistries");

            migrationBuilder.DropTable(
                name: "SatelliteNodes");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Procument.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFollowUpMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerFollowUpMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerFollowUpId = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SentBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFollowUpMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerFollowUpMessages_CustomerFollowUpDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "CustomerFollowUpDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CustomerFollowUpMessages_CustomerFollowUps_CustomerFollowUpId",
                        column: x => x.CustomerFollowUpId,
                        principalTable: "CustomerFollowUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUpMessages_CustomerFollowUpId_SentAt",
                table: "CustomerFollowUpMessages",
                columns: new[] { "CustomerFollowUpId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUpMessages_DocumentId",
                table: "CustomerFollowUpMessages",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerFollowUpMessages");
        }
    }
}

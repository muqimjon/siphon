using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siphon.Accounts.Data.Migrations
{
    /// <inheritdoc />
    public partial class TelegramConnectCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramConnectCodes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramConnectCodes", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramConnectCodes_ExpiresUtc",
                table: "TelegramConnectCodes",
                column: "ExpiresUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramConnectCodes");
        }
    }
}

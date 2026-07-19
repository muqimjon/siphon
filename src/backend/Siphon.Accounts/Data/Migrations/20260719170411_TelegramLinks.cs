using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siphon.Accounts.Data.Migrations
{
    /// <inheritdoc />
    public partial class TelegramLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramLinks",
                columns: table => new
                {
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LinkedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramLinks", x => x.TelegramUserId);
                });

            migrationBuilder.CreateTable(
                name: "TelegramLinkTokens",
                columns: table => new
                {
                    Token = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramLinkTokens", x => x.Token);
                });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DailyRequests", "MaxFileSizeMb" },
                values: new object[] { 100, 500 });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramLinks_UserId",
                table: "TelegramLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramLinkTokens_ExpiresUtc",
                table: "TelegramLinkTokens",
                column: "ExpiresUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramLinks");

            migrationBuilder.DropTable(
                name: "TelegramLinkTokens");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DailyRequests", "MaxFileSizeMb" },
                values: new object[] { 500, 2048 });
        }
    }
}

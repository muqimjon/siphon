using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siphon.Accounts.Data.Migrations
{
    /// <inheritdoc />
    public partial class LimitWindowAndBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Bytes",
                table: "Usage",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ConcurrentLimitOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverridesExpireAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bytes",
                table: "Usage");

            migrationBuilder.DropColumn(
                name: "ConcurrentLimitOverride",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OverridesExpireAt",
                table: "AspNetUsers");
        }
    }
}

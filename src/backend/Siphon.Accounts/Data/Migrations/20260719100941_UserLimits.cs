using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siphon.Accounts.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyRequestLimitOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileSizeLimitMbOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyRequestLimitOverride",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FileSizeLimitMbOverride",
                table: "AspNetUsers");
        }
    }
}

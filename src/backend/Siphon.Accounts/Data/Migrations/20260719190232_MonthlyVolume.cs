using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siphon.Accounts.Data.Migrations
{
    /// <inheritdoc />
    public partial class MonthlyVolume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonthlyGb",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyGbOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                column: "MonthlyGb",
                value: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlyGb",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MonthlyGbOverride",
                table: "AspNetUsers");
        }
    }
}

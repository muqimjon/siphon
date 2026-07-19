using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Siphon.Accounts.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlanCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "DailyRequests", "MaxConcurrent", "MaxFileSizeMb", "MonthlyGb", "Name" },
                values: new object[,]
                {
                    { 2, 500, 4, 2048, 50, "Standard" },
                    { 3, 2000, 8, 8192, 200, "Pro" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}

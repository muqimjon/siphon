using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siphon.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShowPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowPlatform",
                table: "Chats",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowPlatform",
                table: "Chats");
        }
    }
}

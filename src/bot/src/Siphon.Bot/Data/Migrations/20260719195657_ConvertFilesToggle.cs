using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siphon.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertFilesToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ConvertFiles",
                table: "Chats",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE \"Chats\" SET \"ConvertFiles\" = 1 WHERE \"ChatId\" > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConvertFiles",
                table: "Chats");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBotReply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BotReply",
                table: "WhatsAppMessages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotReply",
                table: "WhatsAppMessages");
        }
    }
}

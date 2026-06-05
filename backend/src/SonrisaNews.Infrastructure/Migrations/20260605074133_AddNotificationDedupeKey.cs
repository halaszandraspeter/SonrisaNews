using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaNews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDedupeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Forward-only after merge.
            migrationBuilder.AddColumn<Guid>(
                name: "DedupeKey",
                table: "Notifications",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_ChannelId_DedupeKey",
                table: "Notifications",
                columns: new[] { "ChannelId", "DedupeKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Notifications_ChannelId_DedupeKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DedupeKey",
                table: "Notifications");
        }
    }
}

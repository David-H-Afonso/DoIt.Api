using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskExpiredNotificationPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TaskExpiredEnabled",
                table: "UserNotificationPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TaskExpiredEnabled",
                table: "TaskNotificationOverrides",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskExpiredEnabled",
                table: "UserNotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TaskExpiredEnabled",
                table: "TaskNotificationOverrides");
        }
    }
}

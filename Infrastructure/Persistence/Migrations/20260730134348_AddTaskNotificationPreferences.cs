using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskNotificationOverrides",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AvailableFromEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    RecommendedEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    BeforeAvailableUntilEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    TaskCompletedEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    BeforeAvailableUntilMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskNotificationOverrides", x => x.TaskId);
                    table.ForeignKey(
                        name: "FK_TaskNotificationOverrides_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AvailableFromEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    RecommendedEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    BeforeAvailableUntilEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    TaskCompletedEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    BeforeAvailableUntilMinutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 30),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserNotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskNotificationOverrides");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");
        }
    }
}

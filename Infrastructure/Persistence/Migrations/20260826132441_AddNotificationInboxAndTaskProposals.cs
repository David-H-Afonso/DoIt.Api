using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationInboxAndTaskProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NotificationInboxItemId",
                table: "NotificationDeliveries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushGroupKey",
                table: "NotificationDeliveries",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationInboxItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 220, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DataJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationInboxItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationInboxItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 220, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ZoneId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Importance = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Complexity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Obligation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    AssignmentMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    AssigneeIdsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ScheduleJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResultingTaskId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskProposals_Users_ProposerUserId",
                        column: x => x.ProposerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskProposals_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationInboxItemId",
                table: "NotificationDeliveries",
                column: "NotificationInboxItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationInboxItems_SourceType_SourceId",
                table: "NotificationInboxItems",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationInboxItems_UserId_DeduplicationKey",
                table: "NotificationInboxItems",
                columns: new[] { "UserId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationInboxItems_UserId_ReadAtUtc_CreatedAt",
                table: "NotificationInboxItems",
                columns: new[] { "UserId", "ReadAtUtc", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskProposals_ProposerUserId_CreatedAt",
                table: "TaskProposals",
                columns: new[] { "ProposerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskProposals_TargetUserId_Status_CreatedAt",
                table: "TaskProposals",
                columns: new[] { "TargetUserId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_NotificationInboxItems_NotificationInboxItemId",
                table: "NotificationDeliveries",
                column: "NotificationInboxItemId",
                principalTable: "NotificationInboxItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_NotificationInboxItems_NotificationInboxItemId",
                table: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "NotificationInboxItems");

            migrationBuilder.DropTable(
                name: "TaskProposals");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_NotificationInboxItemId",
                table: "NotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "NotificationInboxItemId",
                table: "NotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "PushGroupKey",
                table: "NotificationDeliveries");
        }
    }
}

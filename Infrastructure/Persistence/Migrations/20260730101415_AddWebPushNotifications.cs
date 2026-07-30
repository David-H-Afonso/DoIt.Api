using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebPushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Auth = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PushSubscriptionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaseUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_PushSubscriptions_PushSubscriptionId",
                        column: x => x.PushSubscriptionId,
                        principalTable: "PushSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_PushSubscriptionId_DeduplicationKey",
                table: "NotificationDeliveries",
                columns: new[] { "PushSubscriptionId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_SourceType_SourceId",
                table: "NotificationDeliveries",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status_NextAttemptAtUtc_LeaseUntilUtc",
                table: "NotificationDeliveries",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_UserId_DueAtUtc",
                table: "NotificationDeliveries",
                columns: new[] { "UserId", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId_IsActive",
                table: "PushSubscriptions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPushGrouping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_PushSubscriptionId_PushGroupKey",
                table: "NotificationDeliveries",
                columns: new[] { "PushSubscriptionId", "PushGroupKey" },
                unique: true,
                filter: "PushGroupKey IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_PushSubscriptionId_PushGroupKey",
                table: "NotificationDeliveries");
        }
    }
}

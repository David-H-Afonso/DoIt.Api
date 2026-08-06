using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskOccurrenceSnoozes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskOccurrenceSnoozes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurrenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UntilAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskOccurrenceSnoozes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskOccurrenceSnoozes_TaskOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "TaskOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskOccurrenceSnoozes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskOccurrenceSnoozes_OccurrenceId_UserId",
                table: "TaskOccurrenceSnoozes",
                columns: new[] { "OccurrenceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskOccurrenceSnoozes_UserId_UntilAtUtc",
                table: "TaskOccurrenceSnoozes",
                columns: new[] { "UserId", "UntilAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskOccurrenceSnoozes");
        }
    }
}

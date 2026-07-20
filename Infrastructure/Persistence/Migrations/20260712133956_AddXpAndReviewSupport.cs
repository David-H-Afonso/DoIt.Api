using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddXpAndReviewSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserXp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TotalXp = table.Column<int>(type: "INTEGER", nullable: false),
                    WeeklyXp = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserXp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserXp_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XpEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurrenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Complexity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Importance = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FormulaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevertedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpEvents_TaskCompletions_CompletionId",
                        column: x => x.CompletionId,
                        principalTable: "TaskCompletions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_XpEvents_TaskOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "TaskOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_XpEvents_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_XpEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserXp_UserId",
                table: "UserXp",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_CompletionId",
                table: "XpEvents",
                column: "CompletionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_OccurrenceId",
                table: "XpEvents",
                column: "OccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_TaskId",
                table: "XpEvents",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_UserId_CreatedAt",
                table: "XpEvents",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserXp");

            migrationBuilder.DropTable(
                name: "XpEvents");
        }
    }
}

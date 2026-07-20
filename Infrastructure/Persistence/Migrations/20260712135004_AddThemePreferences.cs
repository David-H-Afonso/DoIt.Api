using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddThemePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThemePreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThemeMode = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    PrimaryColor = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AccentColor = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BackgroundColor = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SurfaceColor = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TextColor = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    BackgroundImagePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    BackgroundOverlayColor = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BackgroundOverlayOpacity = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemePreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemePreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThemePreferences_UserId",
                table: "ThemePreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThemePreferences");
        }
    }
}

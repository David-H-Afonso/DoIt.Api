using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdConnectionProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    GrantedScopes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    JwtId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdAccessTokens_HouseholdConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "HouseholdConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdAuthorizationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RedirectUri = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CodeChallenge = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdAuthorizationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdAuthorizationCodes_HouseholdConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "HouseholdConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdRefreshTokens_HouseholdConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "HouseholdConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAccessTokens_ConnectionId_FamilyId_ExpiresAt",
                table: "HouseholdAccessTokens",
                columns: new[] { "ConnectionId", "FamilyId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAccessTokens_JwtId",
                table: "HouseholdAccessTokens",
                column: "JwtId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAccessTokens_TokenHash",
                table: "HouseholdAccessTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAuthorizationCodes_CodeHash",
                table: "HouseholdAuthorizationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAuthorizationCodes_ConnectionId_ExpiresAt",
                table: "HouseholdAuthorizationCodes",
                columns: new[] { "ConnectionId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdConnections_UserId_ClientId_Status",
                table: "HouseholdConnections",
                columns: new[] { "UserId", "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRefreshTokens_ConnectionId_FamilyId_ExpiresAt",
                table: "HouseholdRefreshTokens",
                columns: new[] { "ConnectionId", "FamilyId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRefreshTokens_TokenHash",
                table: "HouseholdRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdAccessTokens");

            migrationBuilder.DropTable(
                name: "HouseholdAuthorizationCodes");

            migrationBuilder.DropTable(
                name: "HouseholdRefreshTokens");

            migrationBuilder.DropTable(
                name: "HouseholdConnections");
        }
    }
}

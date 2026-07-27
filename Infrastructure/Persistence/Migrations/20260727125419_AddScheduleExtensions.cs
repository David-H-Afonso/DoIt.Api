using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExtendsUntilNextOccurrence",
                table: "TaskSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtendsUntilNextOccurrence",
                table: "TaskSchedules");
        }
    }
}

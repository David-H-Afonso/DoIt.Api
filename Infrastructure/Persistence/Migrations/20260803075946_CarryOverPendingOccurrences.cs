using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoIt.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CarryOverPendingOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "ExtendsUntilNextOccurrence",
                table: "TaskSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: false);

            migrationBuilder.Sql("UPDATE \"TaskSchedules\" SET \"ExtendsUntilNextOccurrence\" = 1 WHERE \"RecurrenceType\" <> 'Manual';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"TaskSchedules\" SET \"ExtendsUntilNextOccurrence\" = 0 WHERE \"RecurrenceType\" <> 'Manual';");

            migrationBuilder.AlterColumn<bool>(
                name: "ExtendsUntilNextOccurrence",
                table: "TaskSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);
        }
    }
}

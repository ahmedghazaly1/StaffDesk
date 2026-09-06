using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartBMustCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurrenceOccurrences_RuleId",
                table: "RecurrenceOccurrences");

            migrationBuilder.AddColumn<int>(
                name: "BlockedPauseMinutes",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceOccurrences_RuleId_OccurrenceDate",
                table: "RecurrenceOccurrences",
                columns: new[] { "RuleId", "OccurrenceDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurrenceOccurrences_RuleId_OccurrenceDate",
                table: "RecurrenceOccurrences");

            migrationBuilder.DropColumn(
                name: "BlockedPauseMinutes",
                table: "Tasks");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceOccurrences_RuleId",
                table: "RecurrenceOccurrences",
                column: "RuleId");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TaskListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_DeletedAt",
                table: "Tasks",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Status",
                table: "Tasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_DeletedAt",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_Status",
                table: "Tasks");
        }
    }
}

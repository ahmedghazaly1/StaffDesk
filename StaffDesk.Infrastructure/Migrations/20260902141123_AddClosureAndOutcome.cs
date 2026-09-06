using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClosureAndOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClosureRequired",
                table: "Tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "Tasks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskClosures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    IsSlaBreached = table.Column<bool>(type: "boolean", nullable: false),
                    WasReopened = table.Column<bool>(type: "boolean", nullable: false),
                    HasOverriddenAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    ClosureNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskClosures_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskOutcomes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    ExternalReference = table.Column<string>(type: "text", nullable: true),
                    ExternalReferenceLabel = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskOutcomes_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskClosures_TaskId",
                table: "TaskClosures",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskOutcomes_Outcome",
                table: "TaskOutcomes",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_TaskOutcomes_TaskId",
                table: "TaskOutcomes",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskClosures");

            migrationBuilder.DropTable(
                name: "TaskOutcomes");

            migrationBuilder.DropColumn(
                name: "IsClosureRequired",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "Tasks");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaAndEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BreachState",
                table: "Tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSlaEvaluationAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionTargetAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseTargetAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SlaEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaEscalations_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    ResponseTargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    ResolutionTargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaPolicies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaEscalations_TaskId_Level",
                table: "SlaEscalations",
                columns: new[] { "TaskId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_DepartmentId_Priority",
                table: "SlaPolicies",
                columns: new[] { "DepartmentId", "Priority" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaEscalations");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "BreachState",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "LastSlaEvaluationAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ResolutionTargetAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ResponseTargetAt",
                table: "Tasks");
        }
    }
}

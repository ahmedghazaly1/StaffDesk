using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartBBackendGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "DefaultAcceptanceCriteria",
                table: "TaskTemplates",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "NonWorkingDayPolicy",
                table: "RecurrenceRules",
                type: "text",
                nullable: false,
                defaultValue: "NEXT_WORKING_DAY");

            migrationBuilder.CreateTable(
                name: "DepartmentTriagers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentTriagers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentTriagers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentTriagers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    Visibility = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    FilterJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViews_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SavedViews_Employees_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskSlaRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    ResponseTargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    ResolutionTargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSlaRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskSlaRecords_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentTriagers_DepartmentId_EmployeeId",
                table: "DepartmentTriagers",
                columns: new[] { "DepartmentId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentTriagers_EmployeeId",
                table: "DepartmentTriagers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_State_NextAttemptAt",
                table: "Jobs",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Type",
                table: "Jobs",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_DepartmentId",
                table: "SavedViews",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_OwnerId_Name",
                table: "SavedViews",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskSlaRecords_TaskId_EffectiveFrom",
                table: "TaskSlaRecords",
                columns: new[] { "TaskId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentTriagers");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropTable(
                name: "TaskSlaRecords");

            migrationBuilder.DropColumn(
                name: "DefaultAcceptanceCriteria",
                table: "TaskTemplates");

            migrationBuilder.DropColumn(
                name: "NonWorkingDayPolicy",
                table: "RecurrenceRules");
        }
    }
}

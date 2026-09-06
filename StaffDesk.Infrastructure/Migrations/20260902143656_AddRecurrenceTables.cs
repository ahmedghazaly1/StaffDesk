using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrenceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TitlePattern = table.Column<string>(type: "text", nullable: false),
                    DefaultDescription = table.Column<string>(type: "text", nullable: true),
                    DefaultPriority = table.Column<string>(type: "text", nullable: false),
                    DefaultAssigneeId = table.Column<int>(type: "integer", nullable: true),
                    DefaultAssignmentRule = table.Column<string>(type: "text", nullable: true),
                    DefaultEstimateMinutes = table.Column<int>(type: "integer", nullable: true),
                    DefaultTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    DefaultChecklistItems = table.Column<List<string>>(type: "text[]", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTemplates_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskTemplates_Employees_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskTemplates_Employees_DefaultAssigneeId",
                        column: x => x.DefaultAssigneeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecurrenceRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "text", nullable: true),
                    DayOfMonth = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Timezone = table.Column<string>(type: "text", nullable: false),
                    GenerateOnlyWhenPreviousComplete = table.Column<bool>(type: "boolean", nullable: false),
                    IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                    LastGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextGenerationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurrenceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurrenceRules_TaskTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "TaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurrenceOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleId = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<int>(type: "integer", nullable: true),
                    OccurrenceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurrenceOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurrenceOccurrences_RecurrenceRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "RecurrenceRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurrenceOccurrences_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceOccurrences_OccurrenceDate",
                table: "RecurrenceOccurrences",
                column: "OccurrenceDate");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceOccurrences_RuleId",
                table: "RecurrenceOccurrences",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceOccurrences_State",
                table: "RecurrenceOccurrences",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceOccurrences_TaskId",
                table: "RecurrenceOccurrences",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceRules_IsPaused",
                table: "RecurrenceRules",
                column: "IsPaused");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceRules_NextGenerationAt",
                table: "RecurrenceRules",
                column: "NextGenerationAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecurrenceRules_TemplateId",
                table: "RecurrenceRules",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_CreatedById",
                table: "TaskTemplates",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_DefaultAssigneeId",
                table: "TaskTemplates",
                column: "DefaultAssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_DepartmentId",
                table: "TaskTemplates",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_IsActive",
                table: "TaskTemplates",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurrenceOccurrences");

            migrationBuilder.DropTable(
                name: "RecurrenceRules");

            migrationBuilder.DropTable(
                name: "TaskTemplates");
        }
    }
}

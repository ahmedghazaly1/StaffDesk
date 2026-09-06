using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkCyclePartB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceRequestId",
                table: "Tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepartmentDefaultCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentDefaultCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentDefaultCriteria_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskAcceptanceCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsMet = table.Column<bool>(type: "boolean", nullable: false),
                    MetById = table.Column<int>(type: "integer", nullable: true),
                    MetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAcceptanceCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAcceptanceCriteria_Employees_MetById",
                        column: x => x.MetById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskAcceptanceCriteria_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    BusinessJustification = table.Column<string>(type: "text", nullable: true),
                    DesiredByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DeclineReasonCategory = table.Column<string>(type: "text", nullable: true),
                    DeclineNote = table.Column<string>(type: "text", nullable: true),
                    MergedIntoRequestId = table.Column<int>(type: "integer", nullable: true),
                    CreatedTaskId = table.Column<int>(type: "integer", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriageDecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskRequests_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskRequests_Employees_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskRequests_TaskRequests_MergedIntoRequestId",
                        column: x => x.MergedIntoRequestId,
                        principalTable: "TaskRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskRequests_Tasks_CreatedTaskId",
                        column: x => x.CreatedTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskStatusIntervals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    WorkingHoursDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStatusIntervals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskStatusIntervals_Employees_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskStatusIntervals_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_SourceRequestId",
                table: "Tasks",
                column: "SourceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentDefaultCriteria_DepartmentId",
                table: "DepartmentDefaultCriteria",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAcceptanceCriteria_MetById",
                table: "TaskAcceptanceCriteria",
                column: "MetById");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAcceptanceCriteria_TaskId",
                table: "TaskAcceptanceCriteria",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRequests_CreatedTaskId",
                table: "TaskRequests",
                column: "CreatedTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRequests_DepartmentId_Status",
                table: "TaskRequests",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskRequests_MergedIntoRequestId",
                table: "TaskRequests",
                column: "MergedIntoRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRequests_RequestedById",
                table: "TaskRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRequests_Status",
                table: "TaskRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusIntervals_ActorId",
                table: "TaskStatusIntervals",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusIntervals_TaskId",
                table: "TaskStatusIntervals",
                column: "TaskId",
                unique: true,
                filter: "\"ExitedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusIntervals_TaskId_EnteredAt",
                table: "TaskStatusIntervals",
                columns: new[] { "TaskId", "EnteredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskRequests_SourceRequestId",
                table: "Tasks",
                column: "SourceRequestId",
                principalTable: "TaskRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskRequests_SourceRequestId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "DepartmentDefaultCriteria");

            migrationBuilder.DropTable(
                name: "TaskAcceptanceCriteria");

            migrationBuilder.DropTable(
                name: "TaskRequests");

            migrationBuilder.DropTable(
                name: "TaskStatusIntervals");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_SourceRequestId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SourceRequestId",
                table: "Tasks");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartCCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimesheetId",
                table: "TaskTimeEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkCalendarId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPartialDay = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    DecidedById = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Timesheets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Timesheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Timesheets_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Timesheets_Employees_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    WorkDaysMask = table.Column<int>(type: "integer", nullable: false),
                    WorkStartHour = table.Column<int>(type: "integer", nullable: false),
                    WorkEndHour = table.Column<int>(type: "integer", nullable: false),
                    IsOrganizationDefault = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalendarId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RecursAnnually = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarHolidays_WorkCalendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "WorkCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimeEntries_TimesheetId",
                table: "TaskTimeEntries",
                column: "TimesheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_WorkCalendarId",
                table: "Departments",
                column: "WorkCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarHolidays_CalendarId_Date",
                table: "CalendarHolidays",
                columns: new[] { "CalendarId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_DecidedById",
                table: "LeaveRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_State_StartDate_EndDate",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "State", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Timesheets_EmployeeId_WeekStart",
                table: "Timesheets",
                columns: new[] { "EmployeeId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Timesheets_ReviewedById",
                table: "Timesheets",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Timesheets_State",
                table: "Timesheets",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendars_IsOrganizationDefault",
                table: "WorkCalendars",
                column: "IsOrganizationDefault");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_WorkCalendars_WorkCalendarId",
                table: "Departments",
                column: "WorkCalendarId",
                principalTable: "WorkCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskTimeEntries_Timesheets_TimesheetId",
                table: "TaskTimeEntries",
                column: "TimesheetId",
                principalTable: "Timesheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_WorkCalendars_WorkCalendarId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskTimeEntries_Timesheets_TimesheetId",
                table: "TaskTimeEntries");

            migrationBuilder.DropTable(
                name: "CalendarHolidays");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "Timesheets");

            migrationBuilder.DropTable(
                name: "WorkCalendars");

            migrationBuilder.DropIndex(
                name: "IX_TaskTimeEntries_TimesheetId",
                table: "TaskTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_Departments_WorkCalendarId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "TimesheetId",
                table: "TaskTimeEntries");

            migrationBuilder.DropColumn(
                name: "WorkCalendarId",
                table: "Departments");
        }
    }
}

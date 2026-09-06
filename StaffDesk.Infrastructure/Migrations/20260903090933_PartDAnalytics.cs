using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartDAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyMetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WipOpen = table.Column<int>(type: "integer", nullable: false),
                    WipInProgress = table.Column<int>(type: "integer", nullable: false),
                    WipBlocked = table.Column<int>(type: "integer", nullable: false),
                    WipInReview = table.Column<int>(type: "integer", nullable: false),
                    Throughput = table.Column<int>(type: "integer", nullable: false),
                    Arrivals = table.Column<int>(type: "integer", nullable: false),
                    BacklogSize = table.Column<int>(type: "integer", nullable: false),
                    BreachCount = table.Column<int>(type: "integer", nullable: false),
                    AtRiskCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedSampleN = table.Column<int>(type: "integer", nullable: false),
                    LeadTimeP50 = table.Column<double>(type: "double precision", nullable: true),
                    LeadTimeP85 = table.Column<double>(type: "double precision", nullable: true),
                    LeadTimeP95 = table.Column<double>(type: "double precision", nullable: true),
                    CycleTimeP50 = table.Column<double>(type: "double precision", nullable: true),
                    CycleTimeP85 = table.Column<double>(type: "double precision", nullable: true),
                    CycleTimeP95 = table.Column<double>(type: "double precision", nullable: true),
                    IncludesBackfilledIntervals = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMetricSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyMetricSnapshots_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMetricSnapshots_DepartmentId_Date",
                table: "DailyMetricSnapshots",
                columns: new[] { "DepartmentId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMetricSnapshots_DepartmentId_Date_Version",
                table: "DailyMetricSnapshots",
                columns: new[] { "DepartmentId", "Date", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyMetricSnapshots");
        }
    }
}

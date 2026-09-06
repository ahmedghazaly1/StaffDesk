using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApprovalRequired",
                table: "Tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TaskApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskApprovals_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    ApproverId = table.Column<int>(type: "integer", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    DecisionNote = table.Column<string>(type: "text", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TaskApprovalId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_Employees_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_TaskApprovals_TaskApprovalId",
                        column: x => x.TaskApprovalId,
                        principalTable: "TaskApprovals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApproverId",
                table: "ApprovalSteps",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_TaskApprovalId",
                table: "ApprovalSteps",
                column: "TaskApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_TaskId_State",
                table: "ApprovalSteps",
                columns: new[] { "TaskId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskApprovals_TaskId",
                table: "TaskApprovals",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "TaskApprovals");

            migrationBuilder.DropColumn(
                name: "IsApprovalRequired",
                table: "Tasks");
        }
    }
}

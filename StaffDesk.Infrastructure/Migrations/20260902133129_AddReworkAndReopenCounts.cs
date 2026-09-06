using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReworkAndReopenCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalSteps_TaskApprovals_TaskApprovalId",
                table: "ApprovalSteps");

            migrationBuilder.AddColumn<int>(
                name: "ReopenCount",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReworkCount",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TaskApprovalId",
                table: "ApprovalSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ReworkEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    TriggeredBy = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsReopen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReworkEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReworkEvents_Employees_TriggeredBy",
                        column: x => x.TriggeredBy,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReworkEvents_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReworkEvents_Category_OccurredAt",
                table: "ReworkEvents",
                columns: new[] { "Category", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReworkEvents_TaskId",
                table: "ReworkEvents",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ReworkEvents_TriggeredBy",
                table: "ReworkEvents",
                column: "TriggeredBy");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalSteps_TaskApprovals_TaskApprovalId",
                table: "ApprovalSteps",
                column: "TaskApprovalId",
                principalTable: "TaskApprovals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalSteps_TaskApprovals_TaskApprovalId",
                table: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "ReworkEvents");

            migrationBuilder.DropColumn(
                name: "ReopenCount",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ReworkCount",
                table: "Tasks");

            migrationBuilder.AlterColumn<int>(
                name: "TaskApprovalId",
                table: "ApprovalSteps",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalSteps_TaskApprovals_TaskApprovalId",
                table: "ApprovalSteps",
                column: "TaskApprovalId",
                principalTable: "TaskApprovals",
                principalColumn: "Id");
        }
    }
}

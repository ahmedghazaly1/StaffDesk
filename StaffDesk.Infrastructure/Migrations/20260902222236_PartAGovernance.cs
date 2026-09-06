using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartAGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ErasedAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsErased",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DataExports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    SubjectEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    FilterJson = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataExports_Employees_RequestedByEmployeeId",
                        column: x => x.RequestedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalHolds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    PlacedById = table.Column<int>(type: "integer", nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LiftedById = table.Column<int>(type: "integer", nullable: true),
                    LiftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalHolds_Employees_LiftedById",
                        column: x => x.LiftedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalHolds_Employees_PlacedById",
                        column: x => x.PlacedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurgeRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DryRun = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ResultsJson = table.Column<string>(type: "text", nullable: false),
                    CursorJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurgeRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataClass = table.Column<string>(type: "text", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_CreatedAt",
                table: "DataExports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_RequestedByEmployeeId",
                table: "DataExports",
                column: "RequestedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_LiftedById",
                table: "LegalHolds",
                column: "LiftedById");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_PlacedById",
                table: "LegalHolds",
                column: "PlacedById");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_TargetType_TargetId_LiftedAt",
                table: "LegalHolds",
                columns: new[] { "TargetType", "TargetId", "LiftedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_DataClass",
                table: "RetentionPolicies",
                column: "DataClass",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataExports");

            migrationBuilder.DropTable(
                name: "LegalHolds");

            migrationBuilder.DropTable(
                name: "PurgeRuns");

            migrationBuilder.DropTable(
                name: "RetentionPolicies");

            migrationBuilder.DropColumn(
                name: "ErasedAt",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsErased",
                table: "Employees");
        }
    }
}

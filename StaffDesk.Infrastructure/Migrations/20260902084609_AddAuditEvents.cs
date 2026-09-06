using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    ActorLabel = table.Column<string>(type: "text", nullable: false),
                    OnBehalfOfId = table.Column<int>(type: "integer", nullable: true),
                    TargetType = table.Column<string>(type: "text", nullable: true),
                    TargetId = table.Column<string>(type: "text", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    ChangesJson = table.Column<string>(type: "text", nullable: true),
                    RequestId = table.Column<string>(type: "text", nullable: true),
                    SourceIp = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    PrevHash = table.Column<string>(type: "text", nullable: true),
                    Hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Employees_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EventType_Outcome",
                table: "AuditEvents",
                columns: new[] { "EventType", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TargetType_TargetId",
                table: "AuditEvents",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}

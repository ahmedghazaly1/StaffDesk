using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartFPlatformMaturity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PeerInvitations_Token",
                table: "PeerInvitations");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CallerId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeerInvitations_Token",
                table: "PeerInvitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CallerId_Key",
                table: "IdempotencyRecords",
                columns: new[] { "CallerId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ExpiresAt",
                table: "IdempotencyRecords",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_PeerInvitations_Token",
                table: "PeerInvitations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_PeerInvitations_Token",
                table: "PeerInvitations",
                column: "Token",
                unique: true,
                filter: "\"Token\" IS NOT NULL");
        }
    }
}

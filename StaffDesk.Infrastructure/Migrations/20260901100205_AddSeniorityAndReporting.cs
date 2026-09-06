using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeniorityAndReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LevelId",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManagerId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagerId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeniorityLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeniorityLevels", x => x.Id);
                });

            // ============================================
            // NEW: Insert seed data for SeniorityLevels
            // ============================================
            migrationBuilder.InsertData(
                table: "SeniorityLevels",
                columns: new[] { "Name", "Rank", "Description", "IsActive" },
                values: new object[,]
                {
                    { "Intern", 10, "Entry-level position", true },
                    { "Junior", 20, "Junior level", true },
                    { "Mid", 30, "Mid-level", true },
                    { "Senior", 40, "Senior level", true },
                    { "Lead", 50, "Team lead", true },
                    { "Manager", 60, "Manager", true },
                    { "Director", 70, "Director", true }
                });

            // ============================================
            // NEW: Update existing employees to have LevelId = 1 (Intern)
            // This prevents the foreign key error
            // ============================================
            migrationBuilder.Sql(
                @"UPDATE ""Employees"" SET ""LevelId"" = 1 WHERE ""LevelId"" = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LevelId",
                table: "Employees",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ManagerId",
                table: "Departments",
                column: "ManagerId");

            // ============================================
            // Foreign keys are added AFTER data is seeded
            // ============================================
            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Employees_ManagerId",
                table: "Departments",
                column: "ManagerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_SeniorityLevels_LevelId",
                table: "Employees",
                column: "LevelId",
                principalTable: "SeniorityLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Employees_ManagerId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_SeniorityLevels_LevelId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "SeniorityLevels");

            migrationBuilder.DropIndex(
                name: "IX_Employees_LevelId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Departments_ManagerId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Departments");
        }
    }
}
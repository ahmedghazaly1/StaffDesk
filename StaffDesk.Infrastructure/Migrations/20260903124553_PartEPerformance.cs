using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartEPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "JoinedAt",
                table: "Employees",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2020, 1, 1));

            migrationBuilder.CreateTable(
                name: "Competencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    MinSeniorityRank = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ToEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackNotes_Employees_FromEmployeeId",
                        column: x => x.FromEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackNotes_Employees_ToEmployeeId",
                        column: x => x.ToEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OwnerEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ManagerEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    MeasureOfSuccess = table.Column<string>(type: "text", nullable: false),
                    TargetValue = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentValue = table.Column<decimal>(type: "numeric", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    OwnerAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    ManagerAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerAcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManagerAcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Goals_Employees_ManagerEmployeeId",
                        column: x => x.ManagerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Goals_Employees_OwnerEmployeeId",
                        column: x => x.OwnerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartmentIdsJson = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    StageDeadlinesJson = table.Column<string>(type: "text", nullable: false),
                    PeerPresentationMode = table.Column<string>(type: "text", nullable: false),
                    JoinCutOff = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponseWindowDays = table.Column<int>(type: "integer", nullable: false),
                    SkipLogJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    StageChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStageChangeReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetencyLevelDescriptors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetencyId = table.Column<int>(type: "integer", nullable: false),
                    SeniorityLevelId = table.Column<int>(type: "integer", nullable: false),
                    Descriptor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetencyLevelDescriptors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetencyLevelDescriptors_Competencies_CompetencyId",
                        column: x => x.CompetencyId,
                        principalTable: "Competencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetencyLevelDescriptors_SeniorityLevels_SeniorityLevelId",
                        column: x => x.SeniorityLevelId,
                        principalTable: "SeniorityLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalTaskLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoalId = table.Column<int>(type: "integer", nullable: false),
                    TaskId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalTaskLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalTaskLinks_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoalId = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ChangedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalVersions_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewCycleId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ReviewerEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    SelfOverallRating = table.Column<int>(type: "integer", nullable: true),
                    SelfJustification = table.Column<string>(type: "text", nullable: true),
                    SelfSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    SelfSubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SelfScoresJson = table.Column<string>(type: "text", nullable: false),
                    ManagerOverallRating = table.Column<int>(type: "integer", nullable: true),
                    ManagerJustification = table.Column<string>(type: "text", nullable: true),
                    ManagerSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    ManagerSubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManagerScoresJson = table.Column<string>(type: "text", nullable: false),
                    CalibratedOverallRating = table.Column<int>(type: "integer", nullable: true),
                    PreCalibrationOverallRating = table.Column<int>(type: "integer", nullable: true),
                    CalibrationReason = table.Column<string>(type: "text", nullable: true),
                    CalibratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CalibratedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeResponse = table.Column<string>(type: "text", nullable: true),
                    EmployeeResponseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseWindowEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_ReviewerEmployeeId",
                        column: x => x.ReviewerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalTable: "ReviewCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeerInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<int>(type: "integer", nullable: false),
                    NomineeEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    NominatedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerInvitations_Employees_NomineeEmployeeId",
                        column: x => x.NomineeEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerInvitations_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalTable: "PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewAppeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<int>(type: "integer", nullable: false),
                    Ground = table.Column<string>(type: "text", nullable: false),
                    RaisedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    OutcomeReason = table.Column<string>(type: "text", nullable: true),
                    DecidedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewAppeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewAppeals_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalTable: "PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<int>(type: "integer", nullable: false),
                    EvidenceType = table.Column<string>(type: "text", nullable: false),
                    TaskId = table.Column<int>(type: "integer", nullable: true),
                    FeedbackNoteId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    AttachedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewEvidences_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalTable: "PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeerFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeerInvitationId = table.Column<int>(type: "integer", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    ScoresJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerFeedbacks_PeerInvitations_PeerInvitationId",
                        column: x => x.PeerInvitationId,
                        principalTable: "PeerInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competencies_Name",
                table: "Competencies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyLevelDescriptors_CompetencyId_SeniorityLevelId",
                table: "CompetencyLevelDescriptors",
                columns: new[] { "CompetencyId", "SeniorityLevelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyLevelDescriptors_SeniorityLevelId",
                table: "CompetencyLevelDescriptors",
                column: "SeniorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackNotes_FromEmployeeId",
                table: "FeedbackNotes",
                column: "FromEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackNotes_ToEmployeeId",
                table: "FeedbackNotes",
                column: "ToEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_ManagerEmployeeId",
                table: "Goals",
                column: "ManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_OwnerEmployeeId",
                table: "Goals",
                column: "OwnerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalTaskLinks_GoalId_TaskId",
                table: "GoalTaskLinks",
                columns: new[] { "GoalId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoalVersions_GoalId",
                table: "GoalVersions",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerFeedbacks_PeerInvitationId",
                table: "PeerFeedbacks",
                column: "PeerInvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeerInvitations_NomineeEmployeeId",
                table: "PeerInvitations",
                column: "NomineeEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerInvitations_PerformanceReviewId",
                table: "PeerInvitations",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerInvitations_Token",
                table: "PeerInvitations",
                column: "Token",
                unique: true,
                filter: "\"Token\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_EmployeeId",
                table: "PerformanceReviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewCycleId_EmployeeId",
                table: "PerformanceReviews",
                columns: new[] { "ReviewCycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewerEmployeeId",
                table: "PerformanceReviews",
                column: "ReviewerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewAppeals_PerformanceReviewId",
                table: "ReviewAppeals",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_Stage",
                table: "ReviewCycles",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEvidences_PerformanceReviewId",
                table: "ReviewEvidences",
                column: "PerformanceReviewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetencyLevelDescriptors");

            migrationBuilder.DropTable(
                name: "FeedbackNotes");

            migrationBuilder.DropTable(
                name: "GoalTaskLinks");

            migrationBuilder.DropTable(
                name: "GoalVersions");

            migrationBuilder.DropTable(
                name: "PeerFeedbacks");

            migrationBuilder.DropTable(
                name: "ReviewAppeals");

            migrationBuilder.DropTable(
                name: "ReviewEvidences");

            migrationBuilder.DropTable(
                name: "Competencies");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "PeerInvitations");

            migrationBuilder.DropTable(
                name: "PerformanceReviews");

            migrationBuilder.DropTable(
                name: "ReviewCycles");

            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "Employees");
        }
    }
}

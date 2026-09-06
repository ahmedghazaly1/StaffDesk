using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IPerformanceRepository
{
    Task<ReviewCycle> AddCycleAsync(ReviewCycle cycle);
    Task<ReviewCycle?> GetCycleAsync(int id, bool tracking = true);
    Task UpdateCycleAsync(ReviewCycle cycle);

    Task AddReviewsAsync(IEnumerable<PerformanceReview> reviews);
    Task<PerformanceReview?> GetReviewAsync(int id, bool tracking = true);
    Task<IReadOnlyList<PerformanceReview>> GetReviewsForCycleAsync(int cycleId);
    Task<IReadOnlyList<PerformanceReview>> GetReviewsForEmployeeAsync(int employeeId);
    Task UpdateReviewAsync(PerformanceReview review);

    Task<ReviewEvidence> AddEvidenceAsync(ReviewEvidence evidence);
    Task<ReviewAppeal> AddAppealAsync(ReviewAppeal appeal);
    Task<PeerInvitation> AddPeerInvitationAsync(PeerInvitation invitation);
    Task UpdatePeerInvitationAsync(PeerInvitation invitation);
    Task<PeerInvitation?> GetPeerInvitationByTokenAsync(string token, bool tracking = true);
    Task<PeerFeedback> AddPeerFeedbackAsync(PeerFeedback feedback);
    Task<int> CountSubmittedPeersAsync(int reviewId);

    Task<Goal> AddGoalAsync(Goal goal);
    Task<Goal?> GetGoalAsync(int id, bool tracking = true);
    Task UpdateGoalAsync(Goal goal);
    Task AddGoalVersionAsync(GoalVersion version);

    Task<FeedbackNote> AddFeedbackNoteAsync(FeedbackNote note);
    Task<FeedbackNote?> GetFeedbackNoteAsync(int id);

    Task<IReadOnlyList<Competency>> GetCompetenciesAsync();
    Task<Employee?> GetEmployeeAsync(int id);
    Task<IReadOnlyList<Employee>> GetActiveEmployeesInDepartmentsAsync(IEnumerable<int> departmentIds);
    Task<HashSet<int>> GetReportIdsAsync(int managerEmployeeId, bool includeIndirect);
    Task<bool> TaskExistsAsync(int taskId);
    Task SeedCompetenciesIfEmptyAsync(IEnumerable<Competency> competencies);

    /// <summary>PM-37: has this employee acted as reviewer for at least one review in this cycle.</summary>
    Task<bool> HasReviewedInCycleAsync(int reviewerEmployeeId, int cycleId);

    /// <summary>PM-29: current status of each linked task, for aggregate progress display only.</summary>
    Task<Dictionary<int, string>> GetTaskStatusesAsync(IEnumerable<int> taskIds);

    /// <summary>PM-25/26: rework/reopen totals for an employee's completed tasks in a period - a system-level
    /// indicator of how work flowed, never a performance score. Suppressed by the caller below AN-12's threshold.</summary>
    Task<(int SampleSize, int TotalRework, int TotalReopen)> GetQualityIndicatorsAsync(
        int employeeId, DateOnly periodStart, DateOnly periodEnd);
}

public interface IPerformanceService
{
    Task<object> CreateCycleAsync(int actorEmployeeId, string actorRole, CreateReviewCycleRequest req);
    Task<object> AdvanceStageAsync(int actorEmployeeId, string actorRole, int cycleId, AdvanceStageRequest req);
    /// <summary>PM-11: HR_ADMIN may move a cycle back to its previous stage with an audited reason.</summary>
    Task<object> ReverseStageAsync(int actorEmployeeId, string actorRole, int cycleId, AdvanceStageRequest req);
    Task<object> GetProgressAsync(int actorEmployeeId, string actorRole, int cycleId);

    Task<object> GetMyReviewsAsync(int actorEmployeeId, string actorRole);
    Task<object> GetReviewAsync(int actorEmployeeId, string actorRole, int reviewId);
    Task<object> SaveSelfAssessmentAsync(int actorEmployeeId, string actorRole, int reviewId, AssessmentRequest req);
    Task<object> SaveManagerAssessmentAsync(int actorEmployeeId, string actorRole, int reviewId, AssessmentRequest req);
    /// <summary>PM-19: HR_ADMIN reassigns the reviewer when the reporting line is broken or conflicted.</summary>
    Task<object> ReassignReviewerAsync(int actorEmployeeId, string actorRole, int reviewId, ReassignReviewerRequest req);
    Task<object> AttachEvidenceAsync(int actorEmployeeId, string actorRole, int reviewId, AttachEvidenceRequest req);
    Task<object> AddResponseAsync(int actorEmployeeId, string actorRole, int reviewId, ReviewResponseRequest req);
    Task<object> RaiseAppealAsync(int actorEmployeeId, string actorRole, int reviewId, AppealRequest req);
    /// <summary>PM-34: a reviewing authority above the original reviewer resolves an appeal. Appended, never edits the original rating.</summary>
    Task<object> DecideAppealAsync(int actorEmployeeId, string actorRole, int reviewId, int appealId, DecideAppealRequest req);
    Task<object> NominatePeersAsync(int actorEmployeeId, string actorRole, int reviewId, PeerNominationRequest req);

    Task<object> SubmitPeerFeedbackAsync(string token, PeerFeedbackRequest req);

    Task<object> GetCalibrationAsync(int actorEmployeeId, string actorRole, int cycleId, int? departmentId);
    Task<object> CalibrateRatingAsync(int actorEmployeeId, string actorRole, int reviewId, CalibrateRequest req);

    Task<object> CreateGoalAsync(int actorEmployeeId, string actorRole, CreateGoalRequest req);
    Task<object> AcknowledgeGoalAsync(int actorEmployeeId, string actorRole, int goalId);
    Task<object> UpdateGoalProgressAsync(int actorEmployeeId, string actorRole, int goalId, GoalProgressRequest req);

    Task<object> CreateFeedbackNoteAsync(int actorEmployeeId, string actorRole, CreateFeedbackNoteRequest req);
    Task<object> GetCompetenciesAsync();

    /// <summary>PL-4: GET /v1/goals/{id} — returns a goal with an ETag on the response.</summary>
    Task<object?> GetGoalAsync(int actorEmployeeId, string actorRole, int goalId);

    /// <summary>PL-4: the current UpdatedAt a caller needs to compute an If-Match precondition before writing.</summary>
    Task<DateTime?> GetReviewUpdatedAtAsync(int reviewId);
    Task<DateTime?> GetGoalUpdatedAtAsync(int goalId);
}

public sealed class CreateReviewCycleRequest
{
    public string Name { get; set; } = "";
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
    public List<int> DepartmentIds { get; set; } = new();
    public Dictionary<string, string>? StageDeadlines { get; set; }
    public string PeerPresentationMode { get; set; } = PeerPresentationModes.Aggregated;
    public string? JoinCutOff { get; set; }
    public int ResponseWindowDays { get; set; } = 14;
}

public sealed class AdvanceStageRequest
{
    public string? Reason { get; set; }
}

public sealed class AssessmentScoreDto
{
    public int CompetencyId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
}

public sealed class AssessmentRequest
{
    public int OverallRating { get; set; }
    public string Justification { get; set; } = "";
    public List<AssessmentScoreDto> Scores { get; set; } = new();
    public bool Submit { get; set; }
}

public sealed class AttachEvidenceRequest
{
    public string EvidenceType { get; set; } = ReviewEvidenceTypes.Note;
    public int? TaskId { get; set; }
    public int? FeedbackNoteId { get; set; }
    public string? Note { get; set; }
}

public sealed class ReviewResponseRequest
{
    public string Response { get; set; } = "";
}

public sealed class AppealRequest
{
    public string Ground { get; set; } = "";
}

public sealed class DecideAppealRequest
{
    public string Outcome { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class ReassignReviewerRequest
{
    public int NewReviewerEmployeeId { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class PeerNominationRequest
{
    public List<int> NomineeEmployeeIds { get; set; } = new();
    /// <summary>When manager posts pending invitation ids (or nominees), issues them.</summary>
    public List<int>? ApproveInvitationIds { get; set; }
}

public sealed class PeerFeedbackRequest
{
    public int OverallRating { get; set; }
    public string Justification { get; set; } = "";
    public List<AssessmentScoreDto> Scores { get; set; } = new();
}

public sealed class CalibrateRequest
{
    public int OverallRating { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class CreateGoalRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int OwnerEmployeeId { get; set; }
    public int? ManagerEmployeeId { get; set; }
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
    public string MeasureOfSuccess { get; set; } = "";
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal Weight { get; set; } = 1m;
    public List<int>? TaskIds { get; set; }
}

public sealed class GoalProgressRequest
{
    public decimal CurrentValue { get; set; }
    public string? State { get; set; }
    public string? ChangeReason { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? MeasureOfSuccess { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? Weight { get; set; }
}

public sealed class CreateFeedbackNoteRequest
{
    public int ToEmployeeId { get; set; }
    public string Body { get; set; } = "";
}

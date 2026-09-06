namespace StaffDesk.Core.Entities;

public class PerformanceReview
{
    public int Id { get; set; }
    public int ReviewCycleId { get; set; }
    public ReviewCycle ReviewCycle { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int ReviewerEmployeeId { get; set; }
    public Employee Reviewer { get; set; } = null!;

    public int? SelfOverallRating { get; set; }
    public string? SelfJustification { get; set; }
    public bool SelfSubmitted { get; set; }
    public DateTime? SelfSubmittedAt { get; set; }
    /// <summary>JSON array of { competencyId, score, comment }.</summary>
    public string SelfScoresJson { get; set; } = "[]";

    public int? ManagerOverallRating { get; set; }
    public string? ManagerJustification { get; set; }
    public bool ManagerSubmitted { get; set; }
    public DateTime? ManagerSubmittedAt { get; set; }
    public string ManagerScoresJson { get; set; } = "[]";

    public int? CalibratedOverallRating { get; set; }
    public int? PreCalibrationOverallRating { get; set; }
    public string? CalibrationReason { get; set; }
    public DateTime? CalibratedAt { get; set; }
    public int? CalibratedByEmployeeId { get; set; }

    public string? EmployeeResponse { get; set; }
    public DateTime? EmployeeResponseAt { get; set; }
    public DateTime? ResponseWindowEndsAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PeerInvitation> PeerInvitations { get; set; } = new List<PeerInvitation>();
    public ICollection<ReviewEvidence> Evidence { get; set; } = new List<ReviewEvidence>();
    public ICollection<ReviewAppeal> Appeals { get; set; } = new List<ReviewAppeal>();
}

public class ReviewEvidence
{
    public int Id { get; set; }
    public int PerformanceReviewId { get; set; }
    public PerformanceReview PerformanceReview { get; set; } = null!;
    public string EvidenceType { get; set; } = ReviewEvidenceTypes.Note;
    public int? TaskId { get; set; }
    public int? FeedbackNoteId { get; set; }
    public string? Note { get; set; }
    public int AttachedByEmployeeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReviewAppeal
{
    public int Id { get; set; }
    public int PerformanceReviewId { get; set; }
    public PerformanceReview PerformanceReview { get; set; } = null!;
    public string Ground { get; set; } = string.Empty;
    public int RaisedByEmployeeId { get; set; }
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Nullable — no decide endpoint in §8.10.</summary>
    public string? Outcome { get; set; }
    public string? OutcomeReason { get; set; }
    public int? DecidedByEmployeeId { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class PeerInvitation
{
    public int Id { get; set; }
    public int PerformanceReviewId { get; set; }
    public PerformanceReview PerformanceReview { get; set; } = null!;
    public int NomineeEmployeeId { get; set; }
    public Employee Nominee { get; set; } = null!;
    public int NominatedByEmployeeId { get; set; }
    public string Status { get; set; } = PeerInvitationStatuses.Pending;
    public string? Token { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? IssuedAt { get; set; }

    public PeerFeedback? Feedback { get; set; }
}

public class PeerFeedback
{
    public int Id { get; set; }
    public int PeerInvitationId { get; set; }
    public PeerInvitation PeerInvitation { get; set; } = null!;
    public int OverallRating { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string ScoresJson { get; set; } = "[]";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

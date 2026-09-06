namespace StaffDesk.Core.Entities;

public static class ReviewCycleStages
{
    public const string Draft = "DRAFT";
    public const string SelfAssessment = "SELF_ASSESSMENT";
    public const string ManagerAssessment = "MANAGER_ASSESSMENT";
    public const string Calibration = "CALIBRATION";
    public const string Published = "PUBLISHED";
    public const string Closed = "CLOSED";

    public static readonly string[] Order =
    {
        Draft, SelfAssessment, ManagerAssessment, Calibration, Published, Closed
    };

    public static int IndexOf(string stage) => Array.IndexOf(Order, stage);

    public static string? Next(string stage)
    {
        var i = IndexOf(stage);
        return i < 0 || i >= Order.Length - 1 ? null : Order[i + 1];
    }

    public static string? Previous(string stage)
    {
        var i = IndexOf(stage);
        return i <= 0 ? null : Order[i - 1];
    }
}

public static class PeerPresentationModes
{
    public const string Attributed = "ATTRIBUTED";
    public const string Aggregated = "AGGREGATED";
}

public static class PeerInvitationStatuses
{
    public const string Pending = "PENDING";
    public const string Issued = "ISSUED";
    public const string Submitted = "SUBMITTED";
}

public static class GoalStates
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Achieved = "ACHIEVED";
    public const string Partial = "PARTIAL";
    public const string Missed = "MISSED";
    public const string Cancelled = "CANCELLED";
}

public static class RatingScale
{
    public const int Min = 1;
    public const int Max = 5;
    public const int MinJustificationLength = 200;

    public static readonly (int Value, string Label, string Descriptor)[] Descriptors =
    {
        (1, "Not yet meeting", "Consistently below the expectations set for this level; a documented support plan is required."),
        (2, "Partially meeting", "Meets some expectations; specific, named gaps remain."),
        (3, "Meeting", "Reliably meets the expectations of the level. This is the expected outcome for a competent employee, not a disappointment."),
        (4, "Exceeding", "Consistently beyond the level�s expectations, with evidence."),
        (5, "Outstanding", "Performs at the expectations of the level above, sustained across the period.")
    };
}

public static class ReviewEvidenceTypes
{
    public const string Task = "TASK";
    public const string Note = "NOTE";
    public const string FeedbackNote = "FEEDBACK_NOTE";
    public const string Acknowledgement = "ACKNOWLEDGEMENT";
}

public static class PerformanceAuditEvents
{
    public const string ReviewCycleOpened = "REVIEW_CYCLE_OPENED";
    public const string ReviewStageAdvanced = "REVIEW_STAGE_ADVANCED";
    public const string ReviewRead = "REVIEW_READ";
    public const string RatingSubmitted = "RATING_SUBMITTED";
    public const string RatingCalibrated = "RATING_CALIBRATED";
    public const string ReviewPublished = "REVIEW_PUBLISHED";
    public const string AppealRaised = "APPEAL_RAISED";
    public const string AppealDecided = "APPEAL_DECIDED";
    public const string ReviewerReassigned = "REVIEWER_REASSIGNED";
    public const string ReviewStageReversed = "REVIEW_STAGE_REVERSED";
    public const string PeerFeedbackSubmitted = "PEER_FEEDBACK_SUBMITTED";
}

public static class AppealOutcomes
{
    public const string Upheld = "UPHELD";
    public const string Amended = "AMENDED";
    public const string Declined = "DECLINED";

    public static bool IsValid(string? outcome) =>
        outcome is Upheld or Amended or Declined;
}

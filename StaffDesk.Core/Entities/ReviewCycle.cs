namespace StaffDesk.Core.Entities;

public class ReviewCycle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    /// <summary>JSON array of department ids participating in the cycle.</summary>
    public string DepartmentIdsJson { get; set; } = "[]";
    public string Stage { get; set; } = ReviewCycleStages.Draft;
    /// <summary>JSON object of stage ? deadline date (yyyy-MM-dd).</summary>
    public string StageDeadlinesJson { get; set; } = "{}";
    public string PeerPresentationMode { get; set; } = PeerPresentationModes.Aggregated;
    /// <summary>Employees who joined on or after this date are skipped when the cycle opens.</summary>
    public DateOnly JoinCutOff { get; set; }
    public int ResponseWindowDays { get; set; } = 14;
    /// <summary>JSON array of { employeeId, reason } for skipped employees.</summary>
    public string SkipLogJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByEmployeeId { get; set; }
    public DateTime? StageChangedAt { get; set; }
    public string? LastStageChangeReason { get; set; }

    public ICollection<PerformanceReview> Reviews { get; set; } = new List<PerformanceReview>();
}

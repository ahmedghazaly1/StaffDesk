namespace StaffDesk.Core.Entities;

public class Goal
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int OwnerEmployeeId { get; set; }
    public Employee Owner { get; set; } = null!;
    public int ManagerEmployeeId { get; set; }
    public Employee Manager { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string MeasureOfSuccess { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal Weight { get; set; } = 1m;
    public string State { get; set; } = GoalStates.Draft;
    public bool OwnerAcknowledged { get; set; }
    public bool ManagerAcknowledged { get; set; }
    public DateTime? OwnerAcknowledgedAt { get; set; }
    public DateTime? ManagerAcknowledgedAt { get; set; }
    public int CreatedByEmployeeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GoalVersion> Versions { get; set; } = new List<GoalVersion>();
    public ICollection<GoalTaskLink> TaskLinks { get; set; } = new List<GoalTaskLink>();
}

public class GoalVersion
{
    public int Id { get; set; }
    public int GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public string SnapshotJson { get; set; } = "{}";
    public int ChangedByEmployeeId { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GoalTaskLink
{
    public int Id { get; set; }
    public int GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public int TaskId { get; set; }
}

public class FeedbackNote
{
    public int Id { get; set; }
    public int FromEmployeeId { get; set; }
    public Employee FromEmployee { get; set; } = null!;
    public int ToEmployeeId { get; set; }
    public Employee ToEmployee { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

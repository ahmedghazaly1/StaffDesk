namespace StaffDesk.Core.Entities;

public class ReworkEvent
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public string Category { get; set; } = string.Empty; // incomplete, defective, misunderstood, changed, quality

    public string? Note { get; set; }

    public int TriggeredBy { get; set; }
    public Employee TriggeredByEmployee { get; set; } = null!;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // false = rework (IN_REVIEW -> IN_PROGRESS), true = reopen (DONE -> OPEN)
    public bool IsReopen { get; set; } = false;
}
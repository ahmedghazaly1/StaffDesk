namespace StaffDesk.Core.Entities;

public class TaskClosure
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public bool IsSlaBreached { get; set; }  // true if task was breached at closure
    public bool WasReopened { get; set; }    // true if task was reopened at least once
    public bool HasOverriddenAcceptance { get; set; }  // true if acceptance was overridden

    public string? ClosureNote { get; set; }  // Required if SLA breached or reopened

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
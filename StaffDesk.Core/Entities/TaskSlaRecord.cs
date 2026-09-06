namespace StaffDesk.Core.Entities;

// SL-7: one row per (TaskId, EffectiveFrom) - appended (never mutated) whenever the task's
// priority changes (including its very first row, written at task creation), so both the old
// and new targets stay queryable as history. SLA status is always computed from the row with
// the greatest EffectiveFrom for a given task (the clock only moves forward, so "latest row"
// and "latest row with EffectiveFrom <= now" are equivalent here).
public class TaskSlaRecord
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;
    public string Priority { get; set; } = string.Empty;
    public int ResponseTargetMinutes { get; set; }
    public int ResolutionTargetMinutes { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

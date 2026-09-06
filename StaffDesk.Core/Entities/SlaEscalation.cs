namespace StaffDesk.Core.Entities;

// SL-6: one row per (TaskId, Level) that has already been notified. This is both the escalation
// dedup mechanism (a level fires at most once per SLA record) and the AR-3 idempotency mechanism
// for SlaEvaluationJobHandler - re-running the periodic evaluation never re-notifies a level that
// already has a row here.
public class SlaEscalation
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    // 0 = assignee (immediate on breach), 1 = department manager (+60min), 2 = manager's
    // manager (+120min) - see SlaService.EscalationLevels for the authoritative schedule.
    public int Level { get; set; }
    public DateTime NotifiedAt { get; set; } = DateTime.UtcNow;
}

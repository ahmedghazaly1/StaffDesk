namespace StaffDesk.Core.Entities;

// WC-23/WC-24/WC-25/WC-27: one row per contiguous period a task spent in a given Status.
// Exactly one row per task may be "open" (ExitedAt == null) at a time - enforced by a DB
// partial unique index on TaskId (see AppDbContext.OnModelCreating), not just app logic.
public class TaskStatusInterval
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public string Status { get; set; } = string.Empty;

    public DateTime EnteredAt { get; set; }
    public DateTime? ExitedAt { get; set; }

    public int ActorId { get; set; }
    public Employee Actor { get; set; } = null!;

    public int? DurationSeconds { get; set; }

    // WC-24: "working-hours duration computed against the calendar of SS6.1" - that calendar
    // (Part C's WorkCalendar) does not exist in this codebase yet. Column added now for forward
    // compatibility but deliberately left null until Part C introduces the calendar to compute it against.
    public int? WorkingHoursDurationSeconds { get; set; }

    // WC-26: true only for synthetic intervals created by the one-time backfill, never for
    // intervals opened by real create/transition traffic.
    public bool IsEstimated { get; set; } = false;
}

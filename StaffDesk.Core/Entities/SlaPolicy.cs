namespace StaffDesk.Core.Entities;

// SL-1: one row per (DepartmentId, Priority) - response/resolution targets are in *working*
// minutes, always measured against IWorkingCalendarService (SL-2), never wall-clock.
public class SlaPolicy
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string Priority { get; set; } = string.Empty; // URGENT, HIGH, NORMAL, LOW
    public int ResponseTargetMinutes { get; set; }
    public int ResolutionTargetMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

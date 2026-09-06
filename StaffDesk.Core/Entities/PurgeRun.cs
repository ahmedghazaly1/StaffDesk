namespace StaffDesk.Core.Entities;

/// <summary>DG-2/DG-5: tracks a purge (or dry-run) for resumability and operator visibility.</summary>
public class PurgeRun
{
    public long Id { get; set; }
    public bool DryRun { get; set; }
    public string State { get; set; } = "QUEUED"; // QUEUED | RUNNING | SUCCEEDED | FAILED
    public long? JobId { get; set; }
    public int RequestedByEmployeeId { get; set; }
    public string ResultsJson { get; set; } = "{}";
    public string? CursorJson { get; set; } // last processed ids per class for resume (DG-3)
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

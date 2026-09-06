namespace StaffDesk.Core.Entities;

// AR-1..AR-5: minimal in-process job queue. See JobWorker (StaffDesk.API/Jobs) for the polling
// worker and IJobHandler for the pluggable-handler dispatch mechanism.
public class Job
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}"; // jsonb
    public string State { get; set; } = "QUEUED"; // QUEUED, RUNNING, SUCCEEDED, FAILED, DEAD
    public int AttemptCount { get; set; } = 0;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

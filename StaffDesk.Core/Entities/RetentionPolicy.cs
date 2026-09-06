namespace StaffDesk.Core.Entities;

/// <summary>DG-1: retention period per data class, stored as configuration rows.</summary>
public class RetentionPolicy
{
    public int Id { get; set; }
    public string DataClass { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class RetentionDataClasses
{
    public const string AuditEvents = "audit_events";
    public const string TaskActivity = "task_activity";
    public const string Notifications = "notifications";
    public const string SoftDeletedTasks = "soft_deleted_tasks";
    public const string SessionRecords = "session_records";

    /// <summary>PL-3: idempotency records expire on a documented schedule and are purged by this job.</summary>
    public const string IdempotencyKeys = "idempotency_keys";
}

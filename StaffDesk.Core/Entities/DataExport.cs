namespace StaffDesk.Core.Entities;

/// <summary>AU-17 / DG-7: async export jobs (audit log or subject-access pack).</summary>
public class DataExport
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty; // AUDIT | SUBJECT_ACCESS
    public string State { get; set; } = "QUEUED"; // QUEUED | RUNNING | SUCCEEDED | FAILED
    public long? JobId { get; set; }
    public int RequestedByEmployeeId { get; set; }
    public Employee? RequestedBy { get; set; }
    public int? SubjectEmployeeId { get; set; }
    public string FilterJson { get; set; } = "{}";
    public string? FilePath { get; set; }
    public int? RowCount { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public static class DataExportTypes
{
    public const string Audit = "AUDIT";
    public const string SubjectAccess = "SUBJECT_ACCESS";
    public const string Analytics = "ANALYTICS";
}

public static class DataExportStates
{
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

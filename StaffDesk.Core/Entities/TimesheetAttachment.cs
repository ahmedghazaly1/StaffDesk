namespace StaffDesk.Core.Entities;

/// <summary>Supporting document uploaded by the employee and submitted together with a timesheet.</summary>
public class TimesheetAttachment
{
    public int Id { get; set; }

    public int TimesheetId { get; set; }
    public Timesheet Timesheet { get; set; } = null!;

    public int UploadedById { get; set; }
    public Employee UploadedBy { get; set; } = null!;

    /// <summary>Original file name as chosen by the uploader, sanitized of path separators.</summary>
    public string FileName { get; set; } = "";

    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    /// <summary>Path relative to the configured attachment storage root.</summary>
    public string StoragePath { get; set; } = "";

    /// <summary>Hex SHA-256 of the stored bytes, so a download can be checked against what was submitted.</summary>
    public string Sha256 { get; set; } = "";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public static class TimesheetAttachmentRules
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public static readonly string[] AllowedExtensions =
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".zip"
    };
}

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

    /// <summary>Hex SHA-256 of the stored bytes, so a download can be checked against what was submitted.</summary>
    public string Sha256 { get; set; } = "";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public TimesheetAttachmentContent? Content { get; set; }
}

/// <summary>
/// File bytes, kept in their own table so listing timesheets never drags blobs into memory.
/// Stored in the database rather than on disk because the app runs on hosts with an ephemeral
/// container filesystem, where uploaded files would not survive a redeploy.
/// </summary>
public class TimesheetAttachmentContent
{
    /// <summary>Primary key and foreign key: one content row per attachment.</summary>
    public int TimesheetAttachmentId { get; set; }
    public TimesheetAttachment Attachment { get; set; } = null!;

    public byte[] Bytes { get; set; } = Array.Empty<byte>();
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

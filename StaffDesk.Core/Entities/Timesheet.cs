namespace StaffDesk.Core.Entities;

/// <summary>CP-14: monthly timesheet that groups time entries for submit/approve.</summary>
public class Timesheet
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>First day of the month period (UTC date). Column kept as WeekStart for schema compatibility.</summary>
    public DateOnly WeekStart { get; set; }

    /// <summary>OPEN | SUBMITTED | APPROVED | RETURNED</summary>
    public string State { get; set; } = "OPEN";

    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskTimeEntry> Entries { get; set; } = new List<TaskTimeEntry>();
}

public static class TimesheetStates
{
    public const string Open = "OPEN";
    public const string Submitted = "SUBMITTED";
    public const string Approved = "APPROVED";
    public const string Returned = "RETURNED";
}

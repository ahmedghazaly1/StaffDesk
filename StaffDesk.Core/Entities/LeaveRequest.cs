namespace StaffDesk.Core.Entities;

/// <summary>CP-5: leave / absence request.</summary>
public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>annual | sick | unpaid | training | other</summary>
    public string Type { get; set; } = "annual";

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsPartialDay { get; set; }
    public string? Note { get; set; }

    /// <summary>REQUESTED | APPROVED | REJECTED | CANCELLED</summary>
    public string State { get; set; } = "REQUESTED";

    public int? DecidedById { get; set; }
    public Employee? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class LeaveTypes
{
    public static readonly string[] All = { "annual", "sick", "unpaid", "training", "other" };
}

public static class LeaveStates
{
    public const string Requested = "REQUESTED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

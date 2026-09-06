namespace StaffDesk.Core.Entities;

public class Delegation
{
    public int Id { get; set; }

    // Who is delegating (the delegator)
    public int DelegatorId { get; set; }
    public Employee Delegator { get; set; } = null!;

    // Who is receiving the delegation (the delegate)
    public int DelegateId { get; set; }
    public Employee Delegate { get; set; } = null!;

    // What is being delegated
    public string Scope { get; set; } = "ALL";  // ALL, APPROVALS, TASKS

    // Date range
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Reason { get; set; }

    // Who created this delegation (usually the delegator or admin)
    public int CreatedBy { get; set; }
    public Employee CreatedByEmployee { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
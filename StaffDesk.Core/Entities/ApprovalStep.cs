namespace StaffDesk.Core.Entities;

public class ApprovalStep
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public int TaskApprovalId { get; set; }  // ADD THIS
    public TaskApproval TaskApproval { get; set; } = null!;  // ADD THIS

    public int? ApproverId { get; set; }
    public Employee? Approver { get; set; }

    public string? Role { get; set; }

    public int Order { get; set; }

    public string State { get; set; } = "PENDING";

    public string? DecisionNote { get; set; }
    public DateTime? DecisionAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
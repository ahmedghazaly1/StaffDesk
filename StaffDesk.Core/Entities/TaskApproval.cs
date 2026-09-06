namespace StaffDesk.Core.Entities;

public class TaskApproval
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsRequired { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();
}
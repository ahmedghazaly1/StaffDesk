namespace StaffDesk.Core.Entities;

public class TaskChecklistItem
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;  // Changed from Task to WorkTask
    
    public string Label { get; set; } = string.Empty;
    public bool IsDone { get; set; } = false;
    public int Position { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    public int? CompletedById { get; set; }
    public Employee? CompletedBy { get; set; }
}
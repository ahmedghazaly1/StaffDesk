namespace StaffDesk.Core.Entities;

public class TaskTag
{
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;  // Changed from Task to WorkTask
    
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
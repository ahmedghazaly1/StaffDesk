namespace StaffDesk.Core.Entities;

public class TaskWatcher
{
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;  // Changed from Task to WorkTask
    
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
namespace StaffDesk.Core.Entities;

public class TaskDependency
{
    public int Id { get; set; }
    public int BlockedTaskId { get; set; }
    public WorkTask BlockedTask { get; set; } = null!;  // Changed from Task to WorkTask
    
    public int BlockingTaskId { get; set; }
    public WorkTask BlockingTask { get; set; } = null!;  // Changed from Task to WorkTask
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
namespace StaffDesk.Core.Entities;

public class TaskActivity
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;  // Changed from Task to WorkTask
    
    public int ActorId { get; set; }
    public Employee Actor { get; set; } = null!;
    
    public string Action { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
}
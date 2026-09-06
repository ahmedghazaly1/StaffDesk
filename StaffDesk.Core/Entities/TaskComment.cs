namespace StaffDesk.Core.Entities;

public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;  // Changed from Task to WorkTask
    
    public int AuthorId { get; set; }
    public Employee Author { get; set; } = null!;
    
    public string Body { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
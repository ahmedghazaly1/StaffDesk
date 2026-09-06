namespace StaffDesk.Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public int RecipientId { get; set; }
    public Employee Recipient { get; set; } = null!;
    
    public int? TaskId { get; set; }
    public WorkTask? Task { get; set; } = null!;  // Changed from Task to WorkTask
    
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
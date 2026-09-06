namespace StaffDesk.Core.Entities;

public class EmployeeActivity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int ActorUserId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

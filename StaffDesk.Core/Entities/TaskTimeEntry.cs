namespace StaffDesk.Core.Entities;

public class TaskTimeEntry
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;  // Changed from Task to WorkTask
    
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    public int Minutes { get; set; }
    public string? Note { get; set; }
    public DateTime WorkedOn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // CP-14/15: belonging to a weekly timesheet; approved weeks make entries immutable.
    public int? TimesheetId { get; set; }
    public Timesheet? Timesheet { get; set; }
}
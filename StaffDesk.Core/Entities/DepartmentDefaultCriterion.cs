namespace StaffDesk.Core.Entities;

// WC-12: a department's default Definition of Done. Copied onto every new task created in the
// department (as TaskAcceptanceCriterion rows, IsMet=false) at CreateTaskAsync time.
public class DepartmentDefaultCriterion
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

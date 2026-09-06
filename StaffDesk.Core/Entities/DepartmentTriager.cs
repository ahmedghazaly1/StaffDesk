namespace StaffDesk.Core.Entities;

// WC-7: explicitly designated triagers per department (in addition to manager/Admin).
public class DepartmentTriager
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

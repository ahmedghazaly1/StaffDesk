namespace StaffDesk.Core.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int? ManagerId { get; set; }                    
    public Employee? Manager { get; set; } 
    
    // Navigation property - one department has many employees
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    // WC-12: department's default Definition of Done, copied onto every new task in the department.
    public ICollection<DepartmentDefaultCriterion> DefaultCriteria { get; set; } = new List<DepartmentDefaultCriterion>();

    // CP-1: optional department-specific calendar override.
    public int? WorkCalendarId { get; set; }
    public WorkCalendar? WorkCalendar { get; set; }
}
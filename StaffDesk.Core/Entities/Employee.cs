using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Entities;

public class Employee
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    
    public Department? Department { get; set; }
    public int LevelId { get; set; }                      
    public SeniorityLevel Level { get; set; } = null!;    
    public int? ManagerId { get; set; }                   
    public Employee? Manager { get; set; }              
    public bool IsActive { get; set; } = true;

    /// <summary>Employment start date used for review-cycle eligibility (PM-10).</summary>
    public DateOnly JoinedAt { get; set; } = new(2020, 1, 1);

    /// <summary>DG-8: set when identifying fields have been pseudonymised.</summary>
    public bool IsErased { get; set; } = false;
    public DateTime? ErasedAt { get; set; }

    /// <summary>PL-4: source of this employee's ETag for optimistic concurrency.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
}
namespace StaffDesk.Core.Entities;

public class SeniorityLevel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;      
    public int Rank { get; set; }                          
    public string? Description { get; set; }            
    public bool IsActive { get; set; } = true;            
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
namespace StaffDesk.Core.Entities;

// WC-21/22: persisted task-list filter presets.
public class SavedView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public Employee Owner { get; set; } = null!;

    // PRIVATE, DEPARTMENT, ORGANISATION
    public string Visibility { get; set; } = "PRIVATE";
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public string FilterJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

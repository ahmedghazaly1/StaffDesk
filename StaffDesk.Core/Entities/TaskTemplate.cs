namespace StaffDesk.Core.Entities;

public class TaskTemplate
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string TitlePattern { get; set; } = string.Empty;  // e.g., "Weekly Report - {date}"
    public string? DefaultDescription { get; set; }
    public string DefaultPriority { get; set; } = "NORMAL";

    public int? DefaultAssigneeId { get; set; }
    public Employee? DefaultAssignee { get; set; }

    public string? DefaultAssignmentRule { get; set; }  // e.g., "ROTATION", "MANAGER"

    public int? DefaultEstimateMinutes { get; set; }

    public List<string> DefaultTags { get; set; } = new List<string>();
    public List<string> DefaultChecklistItems { get; set; } = new List<string>();
    public List<string> DefaultAcceptanceCriteria { get; set; } = new List<string>();

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int CreatedById { get; set; }
    public Employee CreatedBy { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
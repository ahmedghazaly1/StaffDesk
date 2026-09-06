namespace StaffDesk.Core.Models;

public class SavedViewFilter
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int? AssigneeId { get; set; }
    public int? DepartmentId { get; set; }
    public int? CreatedById { get; set; }
    public bool? WatchedByMe { get; set; }
    public string? Tag { get; set; }
    public string? Search { get; set; }
    public DateTime? DueBefore { get; set; }
    public DateTime? DueAfter { get; set; }
    public bool? Overdue { get; set; }
    public bool? IncludeArchived { get; set; }
    public int? ParentTaskId { get; set; }
    public string? Sort { get; set; }
}

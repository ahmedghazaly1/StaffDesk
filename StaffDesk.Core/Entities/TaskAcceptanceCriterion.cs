namespace StaffDesk.Core.Entities;

// WC-10..WC-14: distinct from TaskChecklistItem. Editable only by task creator, department
// manager, or Admin - never by the assignee alone (see TaskService.CanManageAcceptanceCriteriaAsync).
public class TaskAcceptanceCriterion
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public bool IsMet { get; set; } = false;

    public int? MetById { get; set; }
    public Employee? MetBy { get; set; }
    public DateTime? MetAt { get; set; }

    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

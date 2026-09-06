namespace StaffDesk.Core.Entities;

// WC-1..WC-9: intake/triage. Lifecycle: SUBMITTED -> UNDER_TRIAGE -> (ACCEPTED | DECLINED | MERGED).
public class TaskRequest
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int RequestedById { get; set; }
    public Employee RequestedBy { get; set; } = null!;

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public string? BusinessJustification { get; set; }
    public DateTime? DesiredByDate { get; set; }

    // SUBMITTED, UNDER_TRIAGE, ACCEPTED, DECLINED, MERGED
    public string Status { get; set; } = "SUBMITTED";

    // duplicate, out_of_scope, insufficient_information, not_now, will_not_do
    public string? DeclineReasonCategory { get; set; }
    public string? DeclineNote { get; set; }

    public int? MergedIntoRequestId { get; set; }
    public TaskRequest? MergedIntoRequest { get; set; }

    public int? CreatedTaskId { get; set; }
    public WorkTask? CreatedTask { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TriageDecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

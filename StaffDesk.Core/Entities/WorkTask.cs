using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Entities;

public class WorkTask
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;  // TSK-1042 format
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public string Status { get; set; } = "OPEN";  // OPEN, IN_PROGRESS, BLOCKED, IN_REVIEW, DONE, CANCELLED
    public string Priority { get; set; } = "NORMAL";  // URGENT, HIGH, NORMAL, LOW
    
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    
    public int? AssigneeId { get; set; }
    public Employee? Assignee { get; set; }
    
    public int CreatedById { get; set; }
    public Employee CreatedBy { get; set; } = null!;
    
    public DateTime? DueAt { get; set; }
    public int? EstimateMinutes { get; set; }
    public int LoggedMinutes { get; set; } = 0;
    public int ReworkCount { get; set; } = 0;
    public int ReopenCount { get; set; } = 0;
    public int? ParentTaskId { get; set; }
    public WorkTask? ParentTask { get; set; }

    // WC-6: bidirectional link back to the TaskRequest this task was created from (accept flow), if any.
    public int? SourceRequestId { get; set; }
    public TaskRequest? SourceRequest { get; set; }

    public bool IsArchived { get; set; } = false;
    public bool IsApprovalRequired { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // ============================================
    // SLA Fields (SL-4, SL-7)
    // ============================================
    public DateTime? ResponseTargetAt { get; set; }
    public DateTime? ResolutionTargetAt { get; set; }
    public string? BreachState { get; set; } // ON_TRACK, AT_RISK, BREACHED
    public DateTime? LastSlaEvaluationAt { get; set; }

    // SL-3: accumulated BLOCKED working minutes (paused SLA clock), updated when leaving BLOCKED.
    public int BlockedPauseMinutes { get; set; } = 0;

    // ============================================
    // Closure & Outcome (WC-15..WC-18)
    // ============================================
    public string? Outcome { get; set; }  // DELIVERED, DELIVERED_PARTIAL, SUPERSEDED, NOT_REPRODUCIBLE, DUPLICATE, WONT_DO
    public bool IsClosureRequired { get; set; } = false;  // True if task breached SLA or was reopened
    
    // Navigation properties
    public ICollection<WorkTask> Subtasks { get; set; } = new List<WorkTask>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskWatcher> Watchers { get; set; } = new List<TaskWatcher>();
    public ICollection<TaskTag> Tags { get; set; } = new List<TaskTag>();
    public ICollection<TaskChecklistItem> ChecklistItems { get; set; } = new List<TaskChecklistItem>();
    public ICollection<TaskDependency> BlockedBy { get; set; } = new List<TaskDependency>();
    public ICollection<TaskDependency> Blocking { get; set; } = new List<TaskDependency>();
    public ICollection<TaskTimeEntry> TimeEntries { get; set; } = new List<TaskTimeEntry>();
    public ICollection<TaskActivity> Activities { get; set; } = new List<TaskActivity>();
    public ICollection<TaskStatusInterval> StatusIntervals { get; set; } = new List<TaskStatusInterval>();
    public ICollection<TaskAcceptanceCriterion> AcceptanceCriteria { get; set; } = new List<TaskAcceptanceCriterion>();
}
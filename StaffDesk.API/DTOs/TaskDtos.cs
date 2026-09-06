namespace StaffDesk.API.DTOs;

// ============================================
// Create Task
// ============================================
public class TaskCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DepartmentName { get; set; } = string.Empty;  // Changed from DepartmentId
    public int? AssigneeId { get; set; }
    public string Priority { get; set; } = "NORMAL";
    public DateTime? DueAt { get; set; }
    public int? EstimateMinutes { get; set; }
    public int? ParentTaskId { get; set; }
    public List<string>? Tags { get; set; }
}

// ============================================
// Update Task
// ============================================
public class TaskUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DepartmentName { get; set; } = string.Empty;  // Changed from DepartmentId
    public int? AssigneeId { get; set; }
    public string Priority { get; set; } = "NORMAL";
    public DateTime? DueAt { get; set; }
    public int? EstimateMinutes { get; set; }
    public int? ParentTaskId { get; set; }
    public bool IsArchived { get; set; }
}

// ============================================
// Update Task Status
// ============================================
public class TaskStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }

    // RW-1: explicit rework category (incomplete, defective, misunderstood, changed, quality)
    public string? ReworkCategory { get; set; }

    // TR-18: lets an actor claim an unassigned task in the same request when moving OPEN -> IN_PROGRESS.
    public int? AssigneeId { get; set; }

    // WC-18: required when transitioning to CANCELLED.
    public string? Outcome { get; set; }
}

// ============================================
// Update Task Assignee
// ============================================
public class TaskAssigneeUpdateDto
{
    public int? AssigneeId { get; set; }
}

// ============================================
// Task Response
// ============================================
public class TaskResponseDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Outcome { get; set; }          // NEW
    public bool IsClosureRequired { get; set; }   // NEW
    public string Priority { get; set; } = string.Empty;
    
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    
    public int? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? AssigneeLevel { get; set; }
    
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    
    public DateTime? DueAt { get; set; }
    public int? EstimateMinutes { get; set; }
    public int LoggedMinutes { get; set; }
    public bool IsOverdue { get; set; }
    
    public int? ParentTaskId { get; set; }
    public string? ParentTaskKey { get; set; }
    public string? ParentTaskTitle { get; set; }
    
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public List<string> Tags { get; set; } = new List<string>();
    public int WatcherCount { get; set; }
    public bool IsWatching { get; set; }
    public List<string> AvailableTransitions { get; set; } = new List<string>();
    
    public TaskChecklistSummaryDto Checklist { get; set; } = new TaskChecklistSummaryDto();
    public List<TaskDependencySummaryDto> BlockedBy { get; set; } = new List<TaskDependencySummaryDto>();

    // WC-10..WC-14: mirrors the Checklist summary shape above.
    public TaskAcceptanceCriteriaSummaryDto AcceptanceCriteria { get; set; } = new TaskAcceptanceCriteriaSummaryDto();

    // WC-6: reverse link back to the TaskRequest this task was created from, if any.
    public int? SourceRequestId { get; set; }

    // ============================================
    // RW-3: Rework & Reopen Counts
    // ============================================
    public int ReworkCount { get; set; } = 0;
    public int ReopenCount { get; set; } = 0;

    // ============================================
    // SLA Fields (SL-4, SL-7)
    // ============================================
    public string? BreachState { get; set; }
    public DateTime? ResponseTargetAt { get; set; }
    public DateTime? ResolutionTargetAt { get; set; }
    public long? RemainingMinutes { get; set; }
    public int? BlockedPauseMinutes { get; set; }

    /// <summary>CP-7: non-blocking advisories (e.g. assignee on approved leave).</summary>
    public List<string>? Warnings { get; set; }
}

// ============================================
// Task Checklist Summary
// ============================================
public class TaskChecklistSummaryDto
{
    public int Total { get; set; }
    public int Done { get; set; }
}

// ============================================
// Task Dependency Summary
// ============================================
public class TaskDependencySummaryDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ============================================
// Task List Response (Paginated)
// ============================================
public class TaskListResponseDto
{
    public IEnumerable<TaskResponseDto> Data { get; set; } = new List<TaskResponseDto>();
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

// ============================================
// Task List Query Parameters
// ============================================
public class TaskQueryParams
{
    public int? Page { get; set; } = 1;
    public int? Limit { get; set; } = 25;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int? AssigneeId { get; set; }
    public int? DepartmentId { get; set; }
    public int? CreatedById { get; set; }
    public bool? WatchedByMe { get; set; }
    public string? Tag { get; set; }
    public DateTime? DueBefore { get; set; }
    public DateTime? DueAfter { get; set; }
    public bool? Overdue { get; set; }
    public bool? IncludeArchived { get; set; }
    public int? ParentTaskId { get; set; }
    public string? Sort { get; set; } = "-createdAt";
}
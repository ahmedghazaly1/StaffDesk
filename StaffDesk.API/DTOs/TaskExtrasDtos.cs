namespace StaffDesk.API.DTOs;

// ============================================
// Activity
// ============================================
public class TaskActivityResponseDto
{
    public int Id { get; set; }
    public int ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// Checklist
// ============================================
public class ChecklistItemCreateDto
{
    public string Label { get; set; } = string.Empty;
    public int Position { get; set; } = 0;
}

public class ChecklistItemUpdateDto
{
    public string? Label { get; set; }
    public bool? IsDone { get; set; }
    public int? Position { get; set; }
}

public class ChecklistItemResponseDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int Position { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
}

// ============================================
// Watchers
// ============================================
public class WatcherAddDto
{
    public int? EmployeeId { get; set; }
}

public class WatcherResponseDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ============================================
// Tags
// ============================================
public class TagCreateDto
{
    public string Name { get; set; } = string.Empty;
}

public class TagResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int UsageCount { get; set; }
}

public class TaskTagsReplaceDto
{
    public List<string> Tags { get; set; } = new List<string>();
}

// ============================================
// Dependencies
// ============================================
public class DependencyCreateDto
{
    public int BlockingTaskId { get; set; }
}

public class DependencyResponseDto
{
    public int TaskId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ============================================
// Time Entries
// ============================================
public class TimeEntryCreateDto
{
    public int Minutes { get; set; }
    public DateTime WorkedOn { get; set; }
    public string? Note { get; set; }
}

public class TimeEntryResponseDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int Minutes { get; set; }
    public string? Note { get; set; }
    public DateTime WorkedOn { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// Archive
// ============================================
public class TaskArchiveDto
{
    public bool IsArchived { get; set; }
}

// ============================================
// Department Reporting (RP-1, RP-2)
// ============================================
public class DepartmentTaskSummaryDto
{
    public int DepartmentId { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>();
    public int Overdue { get; set; }
    public int Unassigned { get; set; }
}

public class DepartmentWorkloadEntryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Dictionary<string, int> PriorityCounts { get; set; } = new Dictionary<string, int>();
    public int TotalActive { get; set; }
}

// ============================================
// Employee deactivation (TR-28)
// ============================================
public class DeactivateResponseDto
{
    public bool Deactivated { get; set; }
    public int ActiveTaskCount { get; set; }
    public int? ReassignedTo { get; set; }
}

// ============================================
// Status Duration Tracking (WC-23..WC-27)
// ============================================
public class TaskStatusIntervalResponseDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
    public DateTime? ExitedAt { get; set; }
    public int ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public int? WorkingHoursDurationSeconds { get; set; }
    public bool IsEstimated { get; set; }
}

// ============================================
// Acceptance Criteria / Definition of Done (WC-10..WC-14)
// ============================================
public class AcceptanceCriterionCreateDto
{
    public string Text { get; set; } = string.Empty;
}

public class AcceptanceCriterionUpdateDto
{
    public string? Text { get; set; }
    public bool? IsMet { get; set; }
}

public class AcceptanceCriterionResponseDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public int? MetById { get; set; }
    public string? MetByName { get; set; }
    public DateTime? MetAt { get; set; }
    public int Position { get; set; }
}

public class TaskAcceptanceCriteriaSummaryDto
{
    public int Total { get; set; }
    public int Met { get; set; }
}

// Department default Definition of Done (WC-12)
public class DepartmentDefaultCriterionCreateDto
{
    public string Text { get; set; } = string.Empty;
}

public class DepartmentDefaultCriterionResponseDto
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Position { get; set; }
}

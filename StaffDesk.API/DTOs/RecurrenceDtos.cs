namespace StaffDesk.API.DTOs;

// ============================================
// Create Task Template
// ============================================
public class TaskTemplateCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TitlePattern { get; set; } = string.Empty;
    public string? DefaultDescription { get; set; }
    public string DefaultPriority { get; set; } = "NORMAL";
    public int? DefaultAssigneeId { get; set; }
    public string? DefaultAssignmentRule { get; set; }
    public int? DefaultEstimateMinutes { get; set; }
    public List<string> DefaultTags { get; set; } = new List<string>();
    public List<string> DefaultChecklistItems { get; set; } = new List<string>();
    public List<string> DefaultAcceptanceCriteria { get; set; } = new List<string>();
    public int DepartmentId { get; set; }
}

// ============================================
// Update Task Template
// ============================================
public class TaskTemplateUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TitlePattern { get; set; } = string.Empty;
    public string? DefaultDescription { get; set; }
    public string DefaultPriority { get; set; } = "NORMAL";
    public int? DefaultAssigneeId { get; set; }
    public string? DefaultAssignmentRule { get; set; }
    public int? DefaultEstimateMinutes { get; set; }
    public List<string> DefaultTags { get; set; } = new List<string>();
    public List<string> DefaultChecklistItems { get; set; } = new List<string>();
    public List<string> DefaultAcceptanceCriteria { get; set; } = new List<string>();
    public bool IsActive { get; set; }
}

// ============================================
// Create Recurrence Rule
// ============================================
public class RecurrenceRuleCreateDto
{
    public int TemplateId { get; set; }
    public string Frequency { get; set; } = "DAILY";  // DAILY, WEEKLY, MONTHLY
    public string? DaysOfWeek { get; set; }
    public string? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Timezone { get; set; } = "UTC";
    public bool GenerateOnlyWhenPreviousComplete { get; set; }
    public bool IsPaused { get; set; }
    public string NonWorkingDayPolicy { get; set; } = "NEXT_WORKING_DAY";
}

// ============================================
// Update Recurrence Rule
// ============================================
public class RecurrenceRuleUpdateDto
{
    public string Frequency { get; set; } = "DAILY";
    public string? DaysOfWeek { get; set; }
    public string? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Timezone { get; set; } = "UTC";
    public bool GenerateOnlyWhenPreviousComplete { get; set; }
    public bool IsPaused { get; set; }
    public string NonWorkingDayPolicy { get; set; } = "NEXT_WORKING_DAY";
}

// ============================================
// Template Response
// ============================================
public class TaskTemplateResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TitlePattern { get; set; } = string.Empty;
    public string? DefaultDescription { get; set; }
    public string DefaultPriority { get; set; } = string.Empty;
    public int? DefaultAssigneeId { get; set; }
    public string? DefaultAssigneeName { get; set; }
    public string? DefaultAssignmentRule { get; set; }
    public int? DefaultEstimateMinutes { get; set; }
    public List<string> DefaultTags { get; set; } = new List<string>();
    public List<string> DefaultChecklistItems { get; set; } = new List<string>();
    public List<string> DefaultAcceptanceCriteria { get; set; } = new List<string>();
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============================================
// Recurrence Rule Response
// ============================================
public class RecurrenceRuleResponseDto
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? DaysOfWeek { get; set; }
    public string? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public bool GenerateOnlyWhenPreviousComplete { get; set; }
    public bool IsPaused { get; set; }
    public string NonWorkingDayPolicy { get; set; } = "NEXT_WORKING_DAY";
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime? NextGenerationAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============================================
// Recurrence Occurrence Response
// ============================================
public class RecurrenceOccurrenceResponseDto
{
    public int Id { get; set; }
    public int RuleId { get; set; }
    public int? TaskId { get; set; }
    public string? TaskKey { get; set; }
    public DateTime OccurrenceDate { get; set; }
    public string State { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
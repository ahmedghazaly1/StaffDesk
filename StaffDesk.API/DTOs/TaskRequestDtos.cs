namespace StaffDesk.API.DTOs;

// ============================================
// Task Requests (Intake & Triage, WC-1..WC-9)
// ============================================
public class TaskRequestCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DepartmentId { get; set; }
    public string? BusinessJustification { get; set; }
    public DateTime? DesiredByDate { get; set; }
}

public class TaskRequestDeclineDto
{
    public string ReasonCategory { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public class TaskRequestMergeDto
{
    public int MergedIntoRequestId { get; set; }
}

public class TaskRequestResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    public string? BusinessJustification { get; set; }
    public DateTime? DesiredByDate { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? DeclineReasonCategory { get; set; }
    public string? DeclineNote { get; set; }

    public int? MergedIntoRequestId { get; set; }

    public int? CreatedTaskId { get; set; }
    public string? CreatedTaskKey { get; set; }

    public DateTime SubmittedAt { get; set; }
    public DateTime? TriageDecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// WC-8: triage queue item - same shape plus an "age" field (SubmittedAt -> now).
public class TaskRequestQueueItemDto : TaskRequestResponseDto
{
    public double AgeHours { get; set; }
}

public class TaskRequestListResponseDto
{
    public IEnumerable<TaskRequestResponseDto> Data { get; set; } = new List<TaskRequestResponseDto>();
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

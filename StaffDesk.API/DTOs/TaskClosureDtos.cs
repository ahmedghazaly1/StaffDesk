namespace StaffDesk.API.DTOs;

// ============================================
// Set Task Outcome
// ============================================
public class TaskOutcomeSetDto
{
    public string Outcome { get; set; } = string.Empty;  // DELIVERED, DELIVERED_PARTIAL, SUPERSEDED, NOT_REPRODUCIBLE, DUPLICATE, WONT_DO
    public string? Note { get; set; }
    public string? ExternalReference { get; set; }
    public string? ExternalReferenceLabel { get; set; }
}

// ============================================
// Task Closure
// ============================================
public class TaskClosureDto
{
    public string? ClosureNote { get; set; }
}

// ============================================
// Task Outcome Response
// ============================================
public class TaskOutcomeResponseDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? ExternalReference { get; set; }
    public string? ExternalReferenceLabel { get; set; }
    public DateTime OccurredAt { get; set; }
}

// ============================================
// Task Closure Response
// ============================================
public class TaskClosureResponseDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public bool IsSlaBreached { get; set; }
    public bool WasReopened { get; set; }
    public bool HasOverriddenAcceptance { get; set; }
    public string? ClosureNote { get; set; }
    public DateTime CreatedAt { get; set; }
}
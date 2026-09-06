namespace StaffDesk.API.DTOs;

// ============================================
// Create Approval
// ============================================
public class ApprovalCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = true;
    public List<ApprovalStepCreateDto> Steps { get; set; } = new List<ApprovalStepCreateDto>();
}

public class ApprovalStepCreateDto
{
    public int? ApproverId { get; set; }
    public string? Role { get; set; }
    public int Order { get; set; }
}

// ============================================
// Update Approval Step
// ============================================
public class ApprovalStepUpdateDto
{
    public string State { get; set; } = string.Empty;  // APPROVED, REJECTED
    public string? DecisionNote { get; set; }
}

// ============================================
// Approval Response
// ============================================
public class ApprovalResponseDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public List<ApprovalStepResponseDto> Steps { get; set; } = new List<ApprovalStepResponseDto>();
    public DateTime CreatedAt { get; set; }
    public bool IsFullyApproved { get; set; }
    public bool IsRejected { get; set; }
}

public class ApprovalStepResponseDto
{
    public int Id { get; set; }
    public int? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverLevel { get; set; }
    public string? Role { get; set; }
    public int Order { get; set; }
    public string State { get; set; } = string.Empty;
    public string? DecisionNote { get; set; }
    public DateTime? DecisionAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
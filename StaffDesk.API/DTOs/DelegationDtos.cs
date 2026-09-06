namespace StaffDesk.API.DTOs;

// ============================================
// Create Delegation
// ============================================
public class DelegationCreateDto
{
    public int DelegateId { get; set; }
    public string Scope { get; set; } = "ALL";  // ALL, APPROVALS, TASKS
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Reason { get; set; }
}

// ============================================
// Update Delegation
// ============================================
public class DelegationUpdateDto
{
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

// ============================================
// Delegation Response
// ============================================
public class DelegationResponseDto
{
    public int Id { get; set; }
    public int DelegatorId { get; set; }
    public string DelegatorName { get; set; } = string.Empty;
    public int DelegateId { get; set; }
    public string DelegateName { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
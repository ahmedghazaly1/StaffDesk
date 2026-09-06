namespace StaffDesk.API.DTOs;

// ============================================
// Create SLA Policy
// ============================================
public class SlaPolicyCreateDto
{
    public int DepartmentId { get; set; }
    public string Priority { get; set; } = string.Empty; // URGENT, HIGH, NORMAL, LOW
    public int ResponseTargetMinutes { get; set; }
    public int ResolutionTargetMinutes { get; set; }
}

// ============================================
// Update SLA Policy
// ============================================
public class SlaPolicyUpdateDto
{
    public int ResponseTargetMinutes { get; set; }
    public int ResolutionTargetMinutes { get; set; }
}

// ============================================
// SLA Policy Response
// ============================================
public class SlaPolicyResponseDto
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int ResponseTargetMinutes { get; set; }
    public decimal ResponseTargetHours { get; set; }
    public int ResolutionTargetMinutes { get; set; }
    public decimal ResolutionTargetHours { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============================================
// SLA Task State Response
// ============================================
public class SlaTaskStateResponseDto
{
    public int TaskId { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? ResponseTargetAt { get; set; }
    public DateTime? ResolutionTargetAt { get; set; }
    public string? BreachState { get; set; } // ON_TRACK, AT_RISK, BREACHED
    public long? RemainingMinutes { get; set; }
    public bool IsBreached { get; set; }
    public bool IsAtRisk { get; set; }
    public List<SlaEscalationResponseDto> Escalations { get; set; } = new List<SlaEscalationResponseDto>();
}

// ============================================
// SLA Escalation Response
// ============================================
public class SlaEscalationResponseDto
{
    public int Id { get; set; }
    public int Level { get; set; }
    public DateTime NotifiedAt { get; set; }
    public string Target { get; set; } = string.Empty;
}
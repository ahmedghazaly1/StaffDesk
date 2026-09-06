namespace StaffDesk.Core.Entities;

public class AuditEvent
{
    public long Id { get; set; }
    
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    
    public string EventType { get; set; } = string.Empty;  // From Appendix B catalogue
    
    public int? ActorId { get; set; }  // Null for unauthenticated events
    public Employee? Actor { get; set; }
    
    public string ActorLabel { get; set; } = string.Empty;  // Denormalised name + role at time
    
    public int? OnBehalfOfId { get; set; }  // Effective identity for delegation
    
    public string? TargetType { get; set; }  // e.g., "Task", "Employee", "PerformanceReview"
    public string? TargetId { get; set; }
    
    public string Outcome { get; set; } = "SUCCESS";  // SUCCESS or DENIED
    
    public string? ChangesJson { get; set; }  // Structured before/after
    
    public string? RequestId { get; set; }
    
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    
    public string? PrevHash { get; set; }  // Hash of preceding event
    public string Hash { get; set; } = string.Empty;  // Hash of this event + prevHash
}
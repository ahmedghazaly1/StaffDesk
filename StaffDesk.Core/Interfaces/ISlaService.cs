using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ISlaService
{
    // ============================================
    // Core SLA Methods
    // ============================================
    
    Task<WorkTask> UpdateSlaStateAsync(WorkTask task);
    Task ProcessEscalationsAsync();
    List<(int Level, int DelayMinutes, string Target)> GetEscalationLevels();
    bool IsBreached(WorkTask task);
    long? GetRemainingMinutes(WorkTask task);
    long? GetRemainingMinutes(WorkTask task, int? blockedPauseMinutes);
    Task<int> GetBlockedPauseMinutesAsync(int taskId);
    Task<int> GetBlockedPauseMinutesAsync(WorkTask task);
    Task RecordSlaSnapshotAsync(WorkTask task);

    // ============================================
    // Evaluation Methods (returns results)
    // ============================================
    
    // Evaluate all tasks and return results
    Task<List<SlaEvaluationResult>> EvaluateAllTasksAsync();

    // ============================================
    // Task SLA State
    // ============================================
    
    Task<SlaTaskState> GetTaskSlaStateAsync(WorkTask task);

    // ============================================
    // Audit Logging
    // ============================================
    
    Task LogSlaEventAsync(int actorId, string eventType, string targetId, string outcome, object? changes = null);
}

// ============================================
// Supporting DTOs
// ============================================

public class SlaTaskState
{
    public DateTime? ResponseTargetAt { get; set; }
    public DateTime? ResolutionTargetAt { get; set; }
    public string? BreachState { get; set; }
    public long? RemainingMinutes { get; set; }
    public bool IsBreached { get; set; }
    public bool IsAtRisk { get; set; }
    public List<SlaEscalation> Escalations { get; set; } = new List<SlaEscalation>();
}

public class SlaEvaluationResult
{
    public int TaskId { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string? PreviousState { get; set; }
    public string? NewState { get; set; }
    public int EscalationsSent { get; set; }
    public string? Reason { get; set; }
}
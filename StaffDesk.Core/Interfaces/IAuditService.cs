using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IAuditService
{
    Task<AuditEvent> LogAsync(
        string eventType,
        int? actorId,
        string actorLabel,
        string outcome,
        string? targetType = null,
        string? targetId = null,
        int? onBehalfOfId = null,
        object? changes = null,
        string? requestId = null,
        string? sourceIp = null,
        string? userAgent = null
    );

    Task<AuditEvent?> GetByIdAsync(long id);
    
    Task<(IEnumerable<AuditEvent> Items, string? NextCursor)> QueryAsync(
        string? eventType = null,
        string? outcome = null,
        int? actorId = null,
        string? targetType = null,
        string? targetId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? sourceIp = null,
        string? cursor = null,
        int limit = 50
    );

    Task<(bool IsValid, long? FirstBreakIndex)> VerifyChainAsync(DateTime fromDate, DateTime toDate);
    
    Task<long> GetEventCountAsync(DateTime? fromDate = null, DateTime? toDate = null);
}
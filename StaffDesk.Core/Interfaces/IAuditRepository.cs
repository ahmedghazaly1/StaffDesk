using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IAuditRepository
{
    Task<AuditEvent> CreateAsync(AuditEvent auditEvent);
    Task<AuditEvent?> GetByIdAsync(long id);
    Task<(IEnumerable<AuditEvent> Items, string? NextCursor)> GetFilteredAsync(
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
    Task<AuditEvent?> GetLatestEventAsync();
    Task<(bool IsValid, long? FirstBreakIndex)> VerifyChainAsync(DateTime fromDate, DateTime toDate);
    Task<long> GetEventCountAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<AuditEvent>> GetEventsForEmployeeAsync(int employeeId);

    /// <summary>AU-17: stream matching events for export (no cursor limit).</summary>
    Task<List<AuditEvent>> GetAllMatchingAsync(
        string? eventType,
        string? outcome,
        int? actorId,
        string? targetType,
        string? targetId,
        DateTime? fromDate,
        DateTime? toDate,
        string? sourceIp,
        int maxRows = 100_000);

    /// <summary>DG-2: delete a bounded batch of expired audit events (never PURGE_EXECUTED).</summary>
    Task<int> PurgeExpiredAsync(DateTime cutoffUtc, IReadOnlyCollection<long> exemptIds, int batchSize, long afterId);

    /// <summary>
    /// Recompute every event's Hash/PrevHash from current row content using the
    /// canonical hash (microsecond-truncated OccurredAt). Used to repair chains
    /// written before OccurredAt precision was normalized.
    /// </summary>
    Task<int> RebuildChainHashesAsync();
}

using StaffDesk.Core;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using System.Text.Json;

namespace StaffDesk.Core.Services;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;

    public AuditService(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task<AuditEvent> LogAsync(
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
    string? userAgent = null)
{
    try
    {
        var auditEvent = new AuditEvent
        {
            EventType = eventType,
            ActorId = actorId,
            ActorLabel = actorLabel ?? "Unknown",
            Outcome = outcome ?? "SUCCESS",
            TargetType = targetType,
            TargetId = targetId?.ToString(),
            OnBehalfOfId = onBehalfOfId,
            RequestId = requestId ?? Guid.NewGuid().ToString(),
            SourceIp = sourceIp ?? "unknown",
            UserAgent = userAgent?.Length > 500 ? userAgent.Substring(0, 500) : userAgent,
            // Match Postgres timestamp precision before hashing (see AuditTime).
            OccurredAt = AuditTime.TruncateToMicroseconds(DateTime.UtcNow)
        };

        if (changes != null)
        {
            auditEvent.ChangesJson = System.Text.Json.JsonSerializer.Serialize(changes, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }

        return await _auditRepository.CreateAsync(auditEvent);
    }
    catch (Exception ex)
    {
        // Log the error but don't fail the operation
        Console.WriteLine($"Audit write failed: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        throw new InvalidOperationException($"Failed to write audit event: {ex.Message}", ex);
    }
}

    public async Task<AuditEvent?> GetByIdAsync(long id)
    {
        return await _auditRepository.GetByIdAsync(id);
    }

    public async Task<(IEnumerable<AuditEvent> Items, string? NextCursor)> QueryAsync(
        string? eventType = null,
        string? outcome = null,
        int? actorId = null,
        string? targetType = null,
        string? targetId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? sourceIp = null,
        string? cursor = null,
        int limit = 50)
    {
        return await _auditRepository.GetFilteredAsync(
            eventType, outcome, actorId, targetType, targetId,
            fromDate, toDate, sourceIp, cursor, limit
        );
    }

    public async Task<(bool IsValid, long? FirstBreakIndex)> VerifyChainAsync(DateTime fromDate, DateTime toDate)
    {
        return await _auditRepository.VerifyChainAsync(fromDate, toDate);
    }

    public async Task<long> GetEventCountAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        return await _auditRepository.GetEventCountAsync(fromDate, toDate);
    }
}
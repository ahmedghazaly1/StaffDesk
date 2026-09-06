using Microsoft.EntityFrameworkCore;
using StaffDesk.Core;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StaffDesk.Infrastructure.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AuditEvent> CreateAsync(AuditEvent auditEvent)
    {
        // AU-13: serialise chain writes so concurrent requests cannot claim the same predecessor.
        // PostgreSQL transaction-scoped advisory lock; key is a fixed constant for the audit chain.
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // AU-13: serialise chain writes (PostgreSQL only; other providers rely on the transaction).
            if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(87201401)");

            var previousEvent = await _context.AuditEvents
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            auditEvent.PrevHash = previousEvent?.Hash;
            // Normalize before hashing so the stored row and later verification
            // use the same precision Postgres will persist.
            auditEvent.OccurredAt = AuditTime.TruncateToMicroseconds(auditEvent.OccurredAt);
            auditEvent.Hash = ComputeHash(auditEvent);

            _context.AuditEvents.Add(auditEvent);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return auditEvent;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
    public async Task<AuditEvent?> GetByIdAsync(long id)
    {
        // AsNoTracking: read-only, and prevents EF's automatic navigation fixup between tracked
        // Employees (Manager/DirectReports) from turning the response into a deep/cyclic graph.
        return await _context.AuditEvents
            .AsNoTracking()
            .Include(e => e.Actor)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<(IEnumerable<AuditEvent> Items, string? NextCursor)> GetFilteredAsync(
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
        var query = _context.AuditEvents
            .AsNoTracking()
            .Include(e => e.Actor)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType);

        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(e => e.Outcome == outcome);

        if (actorId.HasValue)
            query = query.Where(e => e.ActorId == actorId.Value);

        if (!string.IsNullOrWhiteSpace(targetType))
            query = query.Where(e => e.TargetType == targetType);

        if (!string.IsNullOrWhiteSpace(targetId))
            query = query.Where(e => e.TargetId == targetId);

        if (fromDate.HasValue)
            query = query.Where(e => e.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.OccurredAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(sourceIp))
            query = query.Where(e => e.SourceIp == sourceIp);

        // Cursor pagination
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var cursorId = long.Parse(cursor);
            query = query.Where(e => e.Id < cursorId);
        }

        var items = await query
            .OrderByDescending(e => e.Id)
            .Take(limit + 1)
            .ToListAsync();

        var nextCursor = items.Count > limit ? items.Last().Id.ToString() : null;
        items = items.Take(limit).ToList();

        return (items, nextCursor);
    }

    public async Task<AuditEvent?> GetLatestEventAsync()
    {
        return await _context.AuditEvents
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();
    }

   public async Task<(bool IsValid, long? FirstBreakIndex)> VerifyChainAsync(DateTime fromDate, DateTime toDate)
{
    var events = await _context.AuditEvents
        .Where(e => e.OccurredAt >= fromDate && e.OccurredAt <= toDate)
        .OrderBy(e => e.Id)
        .ToListAsync();

    if (events.Count == 0) return (true, null);

    for (int i = 0; i < events.Count; i++)
    {
        var current = events[i];
        var expectedHash = ComputeHash(current);
        
        if (current.Hash != expectedHash)
        {
            return (false, current.Id);
        }

        if (i > 0)
        {
            var previous = events[i - 1];
            if (current.PrevHash != previous.Hash)
            {
                return (false, current.Id);
            }
        }
    }

    return (true, null);
}
    public async Task<long> GetEventCountAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.AuditEvents.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(e => e.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.OccurredAt <= toDate.Value);

        return await query.LongCountAsync();
    }

    public async Task<IEnumerable<AuditEvent>> GetEventsForEmployeeAsync(int employeeId)
    {
        return await _context.AuditEvents
            .Where(e => e.ActorId == employeeId || e.OnBehalfOfId == employeeId)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync();
    }

    public async Task<List<AuditEvent>> GetAllMatchingAsync(
        string? eventType,
        string? outcome,
        int? actorId,
        string? targetType,
        string? targetId,
        DateTime? fromDate,
        DateTime? toDate,
        string? sourceIp,
        int maxRows = 100_000)
    {
        var query = _context.AuditEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType);
        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(e => e.Outcome == outcome);
        if (actorId.HasValue)
            query = query.Where(e => e.ActorId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(targetType))
            query = query.Where(e => e.TargetType == targetType);
        if (!string.IsNullOrWhiteSpace(targetId))
            query = query.Where(e => e.TargetId == targetId);
        if (fromDate.HasValue)
            query = query.Where(e => e.OccurredAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(e => e.OccurredAt <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(sourceIp))
            query = query.Where(e => e.SourceIp == sourceIp);

        return await query
            .OrderBy(e => e.Id)
            .Take(maxRows)
            .ToListAsync();
    }

    public async Task<int> PurgeExpiredAsync(DateTime cutoffUtc, IReadOnlyCollection<long> exemptIds, int batchSize, long afterId)
    {
        // DG-2: never remove purge-describing events.
        var query = _context.AuditEvents
            .Where(e => e.Id > afterId
                        && e.OccurredAt < cutoffUtc
                        && e.EventType != "PURGE_EXECUTED");

        if (exemptIds.Count > 0)
            query = query.Where(e => !exemptIds.Contains(e.Id));

        var ids = await query
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .Take(batchSize)
            .ToListAsync();

        if (ids.Count == 0) return 0;

        return await _context.AuditEvents
            .Where(e => ids.Contains(e.Id))
            .ExecuteDeleteAsync();
    }

    public async Task<int> RebuildChainHashesAsync()
    {
        var isNpgsql = _context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        // InMemory (tests) does not support transactions; Postgres needs the advisory lock.
        if (!isNpgsql)
        {
            return await RebuildChainHashesCoreAsync();
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(87201401)");
            var changed = await RebuildChainHashesCoreAsync();
            await tx.CommitAsync();
            return changed;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<int> RebuildChainHashesCoreAsync()
    {
        var events = await _context.AuditEvents
            .OrderBy(e => e.Id)
            .ToListAsync();

        string? prevHash = null;
        var changed = 0;
        foreach (var e in events)
        {
            e.OccurredAt = AuditTime.TruncateToMicroseconds(e.OccurredAt);
            e.PrevHash = prevHash;
            var hash = ComputeHashStatic(e);
            if (e.Hash != hash)
                changed++;
            e.Hash = hash;
            prevHash = hash;
        }

        await _context.SaveChangesAsync();
        return changed;
    }

    /// <summary>AU-12/AU-14: public for tests that deliberately corrupt and re-verify.</summary>
    public static string ComputeHashPublic(AuditEvent auditEvent) => ComputeHashStatic(auditEvent);

    // ============================================
    // Helper: Compute Hash
    // ============================================
    private string ComputeHash(AuditEvent auditEvent) => ComputeHashStatic(auditEvent);

    private static string ComputeHashStatic(AuditEvent auditEvent)
    {
        // Canonical serialization - deterministic order.
        // Id is deliberately excluded: it's a DB-assigned identity column that doesn't exist yet
        // at the point CreateAsync computes this hash (before the INSERT), but does exist by the
        // time VerifyChainAsync recomputes it from a saved row - including it made every single
        // event fail its own verification, unconditionally, which was never caught because AU-14's
        // "verify a corrupted chain" test was never written. PrevHash already anchors this event's
        // position in the chain, so Id isn't needed for tamper-evidence.
        //
        // OccurredAt is truncated to microseconds: Postgres timestamp only stores that
        // precision, so hashing the full .NET tick value at write and the truncated value
        // after a round-trip produced false "chain broken" failures.
        var occurredAt = AuditTime.TruncateToMicroseconds(auditEvent.OccurredAt);
        var canonical = new
        {
            OccurredAt = occurredAt,
            auditEvent.EventType,
            auditEvent.ActorId,
            auditEvent.ActorLabel,
            auditEvent.OnBehalfOfId,
            auditEvent.TargetType,
            auditEvent.TargetId,
            auditEvent.Outcome,
            auditEvent.ChangesJson,
            auditEvent.RequestId,
            auditEvent.SourceIp,
            auditEvent.UserAgent,
            auditEvent.PrevHash
        };

        var json = JsonSerializer.Serialize(canonical, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(json);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
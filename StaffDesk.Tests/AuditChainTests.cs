using Microsoft.EntityFrameworkCore;
using StaffDesk.Core;
using StaffDesk.Core.Entities;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Repositories;

namespace StaffDesk.Tests;

/// <summary>AU-14: verification fails at the exact corrupted event.</summary>
public class AuditChainTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AuditEvent MakeEvent(DateTime occurredAt, string? prevHash, string label, string? requestId = null)
    {
        var e = new AuditEvent
        {
            OccurredAt = occurredAt,
            EventType = "TEST_EVENT",
            ActorLabel = label,
            Outcome = "SUCCESS",
            RequestId = requestId ?? Guid.NewGuid().ToString(),
            PrevHash = prevHash
        };
        e.Hash = AuditRepository.ComputeHashPublic(e);
        return e;
    }

    [Fact]
    public void Hash_Survives_Postgres_Microsecond_Truncation()
    {
        // Sub-microsecond ticks (700 ns) are legal in .NET but not stored by Postgres.
        var fullPrecision = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc).AddTicks(7);
        Assert.NotEqual(0, fullPrecision.Ticks % 10);

        var requestId = Guid.NewGuid().ToString();
        var atWrite = MakeEvent(fullPrecision, null, "actor-0", requestId);

        // Simulate the value after a Postgres round-trip (microseconds only).
        var afterRead = MakeEvent(
            AuditTime.TruncateToMicroseconds(fullPrecision),
            null,
            "actor-0",
            requestId);

        Assert.Equal(atWrite.Hash, afterRead.Hash);
    }

    [Fact]
    public async Task RebuildChainHashes_Repairs_Stale_Hashes()
    {
        await using var context = CreateContext();
        var repo = new AuditRepository(context);

        var e = MakeEvent(DateTime.UtcNow, null, "actor-0");
        e.Hash = "not-the-real-hash";
        context.AuditEvents.Add(e);
        await context.SaveChangesAsync();

        var e2 = MakeEvent(e.OccurredAt.AddMinutes(1), e.Hash, "actor-1");
        context.AuditEvents.Add(e2);
        await context.SaveChangesAsync();

        var from = e.OccurredAt.AddMinutes(-1);
        var to = e2.OccurredAt.AddMinutes(1);
        var (beforeOk, _) = await repo.VerifyChainAsync(from, to);
        Assert.False(beforeOk);

        var changed = await repo.RebuildChainHashesAsync();
        Assert.True(changed >= 1);

        var (afterOk, breakAt) = await repo.VerifyChainAsync(from, to);
        Assert.True(afterOk);
        Assert.Null(breakAt);
    }

    [Fact]
    public async Task VerifyChain_Fails_At_Corrupted_Event()
    {
        await using var context = CreateContext();
        var repo = new AuditRepository(context);

        // InMemory does not support pg_advisory_xact_lock — insert with manual chain for this unit test.
        AuditEvent? prev = null;
        for (var i = 0; i < 3; i++)
        {
            var e = MakeEvent(DateTime.UtcNow.AddMinutes(i), prev?.Hash, $"actor-{i}");
            context.AuditEvents.Add(e);
            await context.SaveChangesAsync();
            prev = e;
        }

        var events = await context.AuditEvents.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(3, events.Count);

        var victim = events[1];
        victim.ChangesJson = "{\"tampered\":true}";
        // Leave Hash as the original — content no longer matches (AU-14).
        await context.SaveChangesAsync();

        var from = events[0].OccurredAt.AddMinutes(-1);
        var to = events[2].OccurredAt.AddMinutes(1);
        var (isValid, firstBreak) = await repo.VerifyChainAsync(from, to);

        Assert.False(isValid);
        Assert.Equal(victim.Id, firstBreak);
    }

    [Fact]
    public async Task VerifyChain_Succeeds_When_Intact()
    {
        await using var context = CreateContext();
        var repo = new AuditRepository(context);

        AuditEvent? prev = null;
        for (var i = 0; i < 3; i++)
        {
            var e = MakeEvent(DateTime.UtcNow.AddMinutes(i), prev?.Hash, $"actor-{i}");
            context.AuditEvents.Add(e);
            await context.SaveChangesAsync();
            prev = e;
        }

        var events = await context.AuditEvents.OrderBy(e => e.Id).ToListAsync();
        var (isValid, firstBreak) = await repo.VerifyChainAsync(
            events[0].OccurredAt.AddMinutes(-1),
            events[2].OccurredAt.AddMinutes(1));

        Assert.True(isValid);
        Assert.Null(firstBreak);
    }
}

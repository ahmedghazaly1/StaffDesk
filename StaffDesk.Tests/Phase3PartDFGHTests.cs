using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Repositories;

namespace StaffDesk.Tests;

/// <summary>
/// Phase 3 Parts D/F/G/H smoke — analytics snapshots, auth lockout, sessions, ops heartbeat, PM cycle open.
/// Part E depth remains in PerformanceTests. Permanent suite — do not delete after a test run.
/// </summary>
public class Phase3PartDFGHTests
{
    [Fact]
    public async Task Auth_Lockout_After_Failures_And_Audits_Account_Locked()
    {
        await using var world = await Phase3World.CreateAsync();
        var auth = world.Auth();

        for (var i = 0; i < 3; i++)
        {
            var fail = await auth.LoginAsync("member", "wrong-password", "127.0.0.1", "test");
            Assert.False(fail.Success);
        }

        var lockedEvents = await world.Db.AuditEvents
            .Where(e => e.EventType == "ACCOUNT_LOCKED")
            .ToListAsync();
        Assert.NotEmpty(lockedEvents);

        var stillLocked = await auth.LoginAsync("member", "pass1234", "127.0.0.1", "test");
        Assert.False(stillLocked.Success);
        Assert.Null(stillLocked.AccessToken);
    }

    [Fact]
    public async Task Login_Success_Issues_Access_And_Refresh()
    {
        await using var world = await Phase3World.CreateAsync();
        var ok = await world.Auth().LoginAsync("manager", "pass1234", "10.0.0.1", "Phase3Tests");
        Assert.True(ok.Success);
        Assert.False(string.IsNullOrWhiteSpace(ok.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(ok.RefreshToken));
        Assert.Equal(User.Roles.Manager, ok.Role);
    }

    [Fact]
    public async Task Session_Refresh_Reuse_Revokes_Family_Via_Harness()
    {
        await using var world = await Phase3World.CreateAsync();
        var sessions = world.Sessions();
        var issued = await sessions.IssueAsync(world.MemberUser, "127.0.0.1", "t");
        await sessions.RotateAsync(issued.RefreshToken, "127.0.0.1", "t");

        var reused = await sessions.RotateAsync(issued.RefreshToken, "127.0.0.1", "t");
        Assert.Null(reused);

        var reuseAudit = await world.Db.AuditEvents
            .AnyAsync(e => e.EventType == "TOKEN_REUSE_DETECTED");
        Assert.True(reuseAudit);
        Assert.False(await sessions.IsSessionActiveAsync(issued.Session.Id));
    }

    [Fact]
    public async Task Operations_Heartbeat_And_Summary_Surface()
    {
        await using var world = await Phase3World.CreateAsync();
        var ops = world.Operations();

        await ops.TouchWorkerHeartbeatAsync("phase3-test-worker");
        var hb = await world.Db.WorkerHeartbeats.SingleAsync(h => h.Id == 1);
        Assert.Equal("phase3-test-worker", hb.InstanceId);

        var summary = await ops.GetSummaryAsync();
        Assert.NotNull(summary.Jobs);
        Assert.NotNull(summary.Readiness);
        Assert.NotNull(summary.AuditChain);
        Assert.Contains(summary.Readiness.Checks, c => c.Name == "worker");
    }

    [Fact]
    public async Task Analytics_Repository_Inserts_Snapshot_Version()
    {
        await using var world = await Phase3World.CreateAsync();
        var repo = new AnalyticsRepository(world.Db);

        var day = new DateOnly(2026, 3, 3);
        var snap = await repo.InsertSnapshotAsync(new DailyMetricSnapshot
        {
            DepartmentId = world.Dept.Id,
            Date = day,
            Version = 1,
            ComputedAt = DateTime.UtcNow,
            Throughput = 3,
            ExtraJson = "{\"note\":\"phase3\"}"
        });

        Assert.True(snap.Id > 0);
        Assert.Equal(1, await repo.GetLatestVersionAsync(world.Dept.Id, day));
        var latest = await repo.GetLatestAsync(world.Dept.Id, day);
        Assert.NotNull(latest);
        Assert.Equal(3, latest!.Throughput);
        Assert.Contains("phase3", latest.ExtraJson);
    }

    [Fact]
    public async Task Performance_HrAdmin_Opens_Draft_Cycle()
    {
        await using var world = await Phase3World.CreateAsync();

        world.ManagerUser.Role = User.Roles.HrAdmin;
        await world.Db.SaveChangesAsync();

        var svc = world.Performance();
        var cycleObj = await svc.CreateCycleAsync(world.Manager.Id, User.Roles.HrAdmin, new CreateReviewCycleRequest
        {
            Name = "Phase3 Cycle",
            PeriodStart = "2026-01-01",
            PeriodEnd = "2026-06-30",
            DepartmentIds = new List<int> { world.Dept.Id },
            JoinCutOff = "2026-01-01",
            ResponseWindowDays = 14
        });

        Assert.Equal(ReviewCycleStages.Draft, cycleObj.GetType().GetProperty("Stage")!.GetValue(cycleObj));
        Assert.Equal("Phase3 Cycle", cycleObj.GetType().GetProperty("Name")!.GetValue(cycleObj));
    }
}

using StaffDesk.Core.Entities;

namespace StaffDesk.Tests;

/// <summary>
/// Phase 3 Part A — audit chain + governance (legal hold / erasure block).
/// Permanent suite — do not delete after a test run.
/// </summary>
public class Phase3PartATests
{
    [Fact]
    public async Task Audit_Log_Appends_Hash_Chained_Events()
    {
        await using var world = await Phase3World.CreateAsync();
        var audit = world.Audit();

        var a = await audit.LogAsync("TEST_A", world.Manager.Id, "employee:" + world.Manager.Id, "SUCCESS");
        var b = await audit.LogAsync("TEST_B", world.Manager.Id, "employee:" + world.Manager.Id, "SUCCESS");

        Assert.False(string.IsNullOrWhiteSpace(a.Hash));
        Assert.Equal(a.Hash, b.PrevHash);

        var (ok, breakAt) = await audit.VerifyChainAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        Assert.True(ok);
        Assert.Null(breakAt);
    }

    [Fact]
    public async Task LegalHold_Blocks_Erasure_Queue()
    {
        await using var world = await Phase3World.CreateAsync();
        var gov = world.Governance();

        var hold = await gov.PlaceLegalHoldAsync(
            LegalHoldTargetTypes.Employee,
            world.Member.Id.ToString(),
            "Litigation hold for member",
            world.Manager.Id);

        Assert.True(hold.IsActive);
        Assert.Null(hold.LiftedAt);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gov.QueueErasureAsync(world.Member.Id, world.Manager.Id));
        Assert.Contains("legal hold", ex.Message, StringComparison.OrdinalIgnoreCase);

        await gov.LiftLegalHoldAsync(hold.Id, world.Manager.Id);
        var (jobId, message) = await gov.QueueErasureAsync(world.Member.Id, world.Manager.Id);
        Assert.True(jobId > 0);
        Assert.Contains("Erasure", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LegalHold_Rejects_Short_Reason()
    {
        await using var world = await Phase3World.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            world.Governance().PlaceLegalHoldAsync(
                LegalHoldTargetTypes.Employee, world.Member.Id.ToString(), "no", world.Manager.Id));
    }

    [Fact]
    public async Task Duplicate_Active_Hold_Is_Rejected()
    {
        await using var world = await Phase3World.CreateAsync();
        var gov = world.Governance();
        await gov.PlaceLegalHoldAsync(
            LegalHoldTargetTypes.Employee, world.Member.Id.ToString(),
            "First hold reason ok", world.Manager.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gov.PlaceLegalHoldAsync(
                LegalHoldTargetTypes.Employee, world.Member.Id.ToString(),
                "Second hold reason ok", world.Manager.Id));
    }
}

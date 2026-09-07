using StaffDesk.Core.Exceptions;

namespace StaffDesk.Tests;

public class DelegationTests
{
    [Fact]
    public async Task Overlap_All_Conflicts_With_Existing_Approvals_Window()
    {
        await using var world = await Phase3World.CreateAsync();
        var delegations = world.Delegation();
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow.AddDays(7);

        await delegations.CreateDelegationAsync(
            world.Manager.Id, world.Peer.Id, "APPROVALS", start, end, "cover", world.Manager.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delegations.CreateDelegationAsync(
                world.Manager.Id, world.Member.Id, "ALL", start, end, "overlap", world.Manager.Id));
    }

    [Fact]
    public async Task Future_Overlapping_Same_Scope_Is_Rejected()
    {
        await using var world = await Phase3World.CreateAsync();
        var delegations = world.Delegation();
        var start = DateTime.UtcNow.AddDays(10);
        var end = DateTime.UtcNow.AddDays(20);

        await delegations.CreateDelegationAsync(
            world.Manager.Id, world.Peer.Id, "TASKS", start, end, "first", world.Manager.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delegations.CreateDelegationAsync(
                world.Manager.Id, world.Member.Id, "TASKS",
                start.AddDays(2), end.AddDays(2), "second", world.Manager.Id));
    }

    [Fact]
    public async Task Approvals_And_Tasks_Scopes_Can_Coexist()
    {
        await using var world = await Phase3World.CreateAsync();
        var delegations = world.Delegation();
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow.AddDays(7);

        await delegations.CreateDelegationAsync(
            world.Manager.Id, world.Peer.Id, "APPROVALS", start, end, null, world.Manager.Id);
        var tasks = await delegations.CreateDelegationAsync(
            world.Manager.Id, world.Member.Id, "TASKS", start, end, null, world.Manager.Id);
        Assert.Equal("TASKS", tasks.Scope);
    }

    [Fact]
    public async Task Delegate_Can_Review_Named_Approval_Step()
    {
        await using var world = await Phase3World.CreateAsync();
        var start = DateTime.UtcNow.AddDays(-1);
        await world.Delegation().CreateDelegationAsync(
            world.Manager.Id, world.Peer.Id, "APPROVALS", start, null, null, world.Manager.Id);

        var task = await world.TaskService().CreateTaskAsync(
            "Needs sign-off", "body", world.Dept.Name, world.Manager.Id, assigneeId: world.Member.Id);
        var approval = await world.Approvals().CreateApprovalAsync(
            task.Id, "Sign-off", null, true,
            new List<(int? ApproverId, string? Role, int Order)> { (world.Manager.Id, null, 1) },
            world.Manager.Id);
        var stepId = approval.Steps.Single().Id;

        var pending = (await world.Approvals().GetPendingApprovalsForUserAsync(world.Peer.Id)).ToList();
        Assert.Contains(pending, s => s.Id == stepId);

        var reviewed = await world.Approvals().ReviewStepAsync(stepId, world.Peer.Id, "APPROVED", "ok");
        Assert.Equal("APPROVED", reviewed.State);
    }

    [Fact]
    public async Task Delegate_Cannot_Approve_Task_Assigned_To_Themselves()
    {
        await using var world = await Phase3World.CreateAsync();
        await world.Delegation().CreateDelegationAsync(
            world.Manager.Id, world.Member.Id, "APPROVALS",
            DateTime.UtcNow.AddDays(-1), null, null, world.Manager.Id);

        var task = await world.TaskService().CreateTaskAsync(
            "Own work", "body", world.Dept.Name, world.Manager.Id, assigneeId: world.Member.Id);
        var approval = await world.Approvals().CreateApprovalAsync(
            task.Id, "Sign-off", null, true,
            new List<(int? ApproverId, string? Role, int Order)> { (world.Manager.Id, null, 1) },
            world.Manager.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Approvals().ReviewStepAsync(approval.Steps.Single().Id, world.Member.Id, "APPROVED", "no"));
        Assert.Contains("assigned", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Role_Step_Requires_Matching_Role()
    {
        await using var world = await Phase3World.CreateAsync();
        var task = await world.TaskService().CreateTaskAsync(
            "Role gate", "body", world.Dept.Name, world.Manager.Id, assigneeId: world.Member.Id);
        var approval = await world.Approvals().CreateApprovalAsync(
            task.Id, "Mgr role", null, true,
            new List<(int? ApproverId, string? Role, int Order)> { (null, "Manager", 1) },
            world.Manager.Id);
        var stepId = approval.Steps.Single().Id;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            world.Approvals().ReviewStepAsync(stepId, world.Member.Id, "APPROVED", "no"));

        var reviewed = await world.Approvals().ReviewStepAsync(stepId, world.Manager.Id, "APPROVED", "yes");
        Assert.Equal("APPROVED", reviewed.State);
    }

    [Fact]
    public async Task Tasks_Delegate_Can_Advance_Assigned_Work()
    {
        await using var world = await Phase3World.CreateAsync();
        await world.Delegation().CreateDelegationAsync(
            world.OtherDeptMember.Id, world.Member.Id, "TASKS",
            DateTime.UtcNow.AddDays(-1), null, null, world.OtherDeptMember.Id);

        var tasks = world.TaskService();
        var task = await tasks.CreateTaskAsync(
            "Cover my board", "body", world.OtherDept.Name, world.OtherDeptMember.Id,
            assigneeId: world.OtherDeptMember.Id);

        Assert.True(await tasks.CanEditTaskAsync(task.Id, world.Member.Id));
        var moved = await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_PROGRESS");
        Assert.Equal("IN_PROGRESS", moved.Status);
    }

    [Fact]
    public async Task Tasks_Delegate_Cannot_Self_Complete_Review()
    {
        await using var world = await Phase3World.CreateAsync();
        await world.Delegation().CreateDelegationAsync(
            world.Member.Id, world.Peer.Id, "TASKS",
            DateTime.UtcNow.AddDays(-1), null, null, world.Member.Id);

        var tasks = world.TaskService();
        var task = await tasks.CreateTaskAsync(
            "Must be reviewed", "body", world.Dept.Name, world.Manager.Id, assigneeId: world.Member.Id);
        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_PROGRESS");
        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_REVIEW");

        var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
            tasks.TransitionStatusAsync(task.Id, world.Peer.Id, "DONE"));
        Assert.Equal(403, ex.HttpStatus);
    }

    [Fact]
    public async Task Manager_Who_Is_Tasks_Delegate_Can_Still_Skip_Step()
    {
        await using var world = await Phase3World.CreateAsync();
        await world.Delegation().CreateDelegationAsync(
            world.Member.Id, world.Manager.Id, "TASKS",
            DateTime.UtcNow.AddDays(-1), null, null, world.Member.Id);

        var task = await world.TaskService().CreateTaskAsync(
            "Skip me", "body", world.Dept.Name, world.Manager.Id, assigneeId: world.Peer.Id);
        var approval = await world.Approvals().CreateApprovalAsync(
            task.Id, "Sign-off", null, true,
            new List<(int? ApproverId, string? Role, int Order)> { (world.Peer.Id, null, 1) },
            world.Manager.Id);

        var skipped = await world.Approvals().SkipStepAsync(approval.Steps.Single().Id, world.Manager.Id, "manager skip");
        Assert.Equal("SKIPPED", skipped.State);
    }
}

using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;

namespace StaffDesk.Tests;

/// <summary>
/// Phase 3 Part B — intake/triage, acceptance criteria gate, rework/reopen, one open interval.
/// Permanent suite — do not delete after a test run.
/// </summary>
public class Phase3PartBTests
{
    [Fact]
    public async Task Triage_Accept_Creates_Task_With_Open_Interval()
    {
        await using var world = await Phase3World.CreateAsync();
        var requests = world.TaskRequests();

        var submitted = await requests.SubmitAsync(
            world.Member.Id, "Need new feature X", "desc", world.Dept.Id, "biz", null);

        Assert.Equal("SUBMITTED", submitted.Status);

        var accepted = await requests.AcceptAsync(submitted.Id, world.Manager.Id);
        Assert.Equal("ACCEPTED", accepted.Status);
        Assert.NotNull(accepted.CreatedTaskId);

        var intervals = await world.Db.TaskStatusIntervals
            .Where(i => i.TaskId == accepted.CreatedTaskId!.Value)
            .ToListAsync();
        Assert.Single(intervals);
        Assert.Null(intervals[0].ExitedAt);
        Assert.Equal("OPEN", intervals[0].Status);

        var createdTask = await world.Db.Tasks.FindAsync(accepted.CreatedTaskId!.Value);
        Assert.Equal(world.Member.Id, createdTask!.AssigneeId);
    }

    [Fact]
    public async Task My_Requests_List_Excludes_Department_Requests_Submitted_By_Others()
    {
        await using var world = await Phase3World.CreateAsync();
        var requests = world.TaskRequests();

        await requests.SubmitAsync(world.Member.Id, "Member submitted this", null, world.Dept.Id, null, null);
        await requests.SubmitAsync(world.Manager.Id, "Manager own request", null, world.Dept.Id, null, null);

        var (managerItems, managerTotal) = await requests.GetListAsync(world.Manager.Id);
        Assert.Equal(1, managerTotal);
        Assert.All(managerItems, r => Assert.Equal(world.Manager.Id, r.RequestedById));
        Assert.Contains(managerItems, r => r.Title == "Manager own request");

        var (memberItems, memberTotal) = await requests.GetListAsync(world.Member.Id);
        Assert.Equal(1, memberTotal);
        Assert.Contains(memberItems, r => r.Title == "Member submitted this");
    }

    [Fact]
    public async Task Submit_Notifies_Department_Manager_And_Admins_Not_Requester()
    {
        await using var world = await Phase3World.CreateAsync();

        var adminEmployee = new Employee
        {
            FullName = "Org Admin", JobTitle = "Admin", DepartmentId = world.Dept.Id,
            LevelId = world.Level.Id, JoinedAt = new DateOnly(2018, 1, 1), IsActive = true
        };
        world.Db.Employees.Add(adminEmployee);
        await world.Db.SaveChangesAsync();
        world.Db.Users.Add(new User
        {
            Username = "admin", Email = "a@t", Role = User.Roles.Admin,
            EmployeeId = adminEmployee.Id, PasswordHash = "x"
        });
        await world.Db.SaveChangesAsync();

        var requests = world.TaskRequests();
        await requests.SubmitAsync(
            world.Member.Id, "Need triage attention", null, world.Dept.Id, null, null);

        var submittedNotes = await world.Db.Notifications
            .Where(n => n.Type == "TASK_REQUEST_SUBMITTED")
            .ToListAsync();

        Assert.Contains(submittedNotes, n => n.RecipientId == world.Manager.Id);
        Assert.Contains(submittedNotes, n => n.RecipientId == adminEmployee.Id);
        Assert.DoesNotContain(submittedNotes, n => n.RecipientId == world.Member.Id);
        Assert.All(submittedNotes, n => Assert.Contains("Need triage attention", n.Message));
    }

    [Fact]
    public async Task Triage_Decline_Requires_Category_And_Note()
    {
        await using var world = await Phase3World.CreateAsync();
        var requests = world.TaskRequests();
        var submitted = await requests.SubmitAsync(
            world.Member.Id, "Out of scope idea", null, world.Dept.Id, null, null);

        var bad = await Assert.ThrowsAsync<TaskDomainException>(() =>
            requests.DeclineAsync(submitted.Id, world.Manager.Id, "duplicate", ""));
        Assert.Equal(400, bad.HttpStatus);

        var declined = await requests.DeclineAsync(
            submitted.Id, world.Manager.Id, "out_of_scope", "Not in roadmap this quarter");
        Assert.Equal("DECLINED", declined.Status);
    }

    [Fact]
    public async Task Member_Cannot_Triage_Department_Queue()
    {
        await using var world = await Phase3World.CreateAsync();
        var requests = world.TaskRequests();
        var submitted = await requests.SubmitAsync(
            world.Peer.Id, "Please triage me", null, world.Dept.Id, null, null);

        var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
            requests.AcceptAsync(submitted.Id, world.Member.Id));
        Assert.Equal(403, ex.HttpStatus);
    }

    [Fact]
    public async Task Acceptance_Criteria_Gate_Blocks_Done_Until_Met()
    {
        await using var world = await Phase3World.CreateAsync();
        var tasks = world.TaskService();

        var task = await tasks.CreateTaskAsync(
            "Ship checklist gate", "body", world.Dept.Name, world.Manager.Id,
            assigneeId: world.Member.Id);

        var criterion = await tasks.AddAcceptanceCriterionAsync(task.Id, world.Manager.Id, "Tests pass");
        Assert.False(criterion.IsMet);

        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_PROGRESS");
        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_REVIEW");

        var blocked = await Assert.ThrowsAsync<TaskDomainException>(() =>
            tasks.TransitionStatusAsync(task.Id, world.Manager.Id, "DONE"));
        Assert.Equal(TaskErrorCodes.AcceptanceCriteriaUnmet, blocked.Code);

        await tasks.UpdateAcceptanceCriterionAsync(criterion.Id, world.Manager.Id, null, true);
        var done = await tasks.TransitionStatusAsync(task.Id, world.Manager.Id, "DONE");
        Assert.Equal("DONE", done.Status);
    }

    [Fact]
    public async Task Rework_And_Reopen_Increment_Counters()
    {
        await using var world = await Phase3World.CreateAsync();
        var tasks = world.TaskService();

        var task = await tasks.CreateTaskAsync(
            "Rework reopen path", null, world.Dept.Name, world.Manager.Id,
            assigneeId: world.Member.Id);

        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_PROGRESS");
        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_REVIEW");

        var reworked = await tasks.TransitionStatusAsync(
            task.Id, world.Manager.Id, "IN_PROGRESS",
            reason: "Needs more coverage", reworkCategory: "quality");
        Assert.Equal(1, reworked.ReworkCount);

        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_REVIEW");
        await tasks.TransitionStatusAsync(task.Id, world.Manager.Id, "DONE");

        var reopened = await tasks.TransitionStatusAsync(
            task.Id, world.Manager.Id, "OPEN",
            reason: "Regression found in prod", reworkCategory: "defective");
        Assert.Equal(1, reopened.ReopenCount);
    }

    [Fact]
    public async Task Status_Transitions_Keep_Exactly_One_Open_Interval()
    {
        await using var world = await Phase3World.CreateAsync();
        var tasks = world.TaskService();

        var task = await tasks.CreateTaskAsync(
            "Interval invariant", null, world.Dept.Name, world.Manager.Id,
            assigneeId: world.Member.Id);

        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_PROGRESS");
        await tasks.TransitionStatusAsync(task.Id, world.Member.Id, "IN_REVIEW");

        var open = await world.Db.TaskStatusIntervals
            .Where(i => i.TaskId == task.Id && i.ExitedAt == null)
            .ToListAsync();
        Assert.Single(open);
        Assert.Equal("IN_REVIEW", open[0].Status);

        var closed = await world.Db.TaskStatusIntervals
            .CountAsync(i => i.TaskId == task.Id && i.ExitedAt != null);
        Assert.Equal(2, closed);
    }
}

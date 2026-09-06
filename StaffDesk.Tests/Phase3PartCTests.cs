using System.Text.Json;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;

namespace StaffDesk.Tests;

/// <summary>
/// Phase 3 Part C — leave (no self-approve), capacity unestimated load, working calendar, SLA breach.
/// Permanent suite — do not delete after a test run.
/// </summary>
public class Phase3PartCTests
{
    [Fact]
    public async Task Leave_Self_Approve_Is_Denied()
    {
        await using var world = await Phase3World.CreateAsync();
        var leave = world.Leave();

        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14));
        var req = await leave.RequestLeaveAsync(
            world.Member.Id, "annual", start, start.AddDays(1), false, "vacation");

        var self = await Assert.ThrowsAsync<TaskDomainException>(() =>
            leave.DecideAsync(req.Id, world.Member.Id, true, null));
        Assert.Equal(403, self.HttpStatus);

        var peer = await Assert.ThrowsAsync<TaskDomainException>(() =>
            leave.DecideAsync(req.Id, world.Peer.Id, true, null));
        Assert.Equal(403, peer.HttpStatus);

        var approved = await leave.DecideAsync(req.Id, world.Manager.Id, true, "ok");
        Assert.Equal(LeaveStates.Approved, approved.State);
        Assert.True(await leave.IsOnApprovedLeaveAsync(world.Member.Id, start));
    }

    [Fact]
    public async Task Capacity_Workload_Counts_Unestimated_Tasks()
    {
        await using var world = await Phase3World.CreateAsync();
        var tasks = world.TaskService();

        await tasks.CreateTaskAsync(
            "Estimated work item", null, world.Dept.Name, world.Manager.Id,
            assigneeId: world.Member.Id, estimateMinutes: 120);
        await tasks.CreateTaskAsync(
            "Unestimated work item", null, world.Dept.Name, world.Manager.Id,
            assigneeId: world.Member.Id);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var to = from.AddDays(7);
        var result = await world.Capacity().GetWorkloadAsync(
            world.Dept.Id, from, to, world.Manager.Id, User.Roles.Manager, 20);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var members = doc.RootElement.GetProperty("members");
        JsonElement? memberRow = null;
        foreach (var m in members.EnumerateArray())
        {
            if (m.GetProperty("employeeId").GetInt32() == world.Member.Id)
                memberRow = m;
        }

        Assert.True(memberRow.HasValue);
        Assert.Equal(1, memberRow.Value.GetProperty("unestimatedTaskCount").GetInt32());
        Assert.Equal(120, memberRow.Value.GetProperty("committedLoadMinutes").GetInt32());
    }

    [Fact]
    public async Task Working_Calendar_Uses_Seeded_Org_Default()
    {
        await using var world = await Phase3World.CreateAsync();
        var svc = world.CalendarService();
        var cal = await svc.ResolveCalendarAsync(world.Dept.Id);

        Assert.Equal(world.Calendar.Id, cal.Id);
        Assert.True(cal.IsOrganizationDefault);

        var mon = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var fri = new DateTime(2026, 3, 6, 17, 0, 0, DateTimeKind.Utc);
        var minutes = svc.GetWorkingMinutes(mon, fri, cal, Array.Empty<CalendarHoliday>());
        Assert.Equal(5 * 8 * 60, minutes);
    }

    [Fact]
    public void Sla_Overdue_Target_Is_Breached()
    {
        var cal = new WorkCalendar
        {
            Name = "UTC",
            TimeZoneId = "UTC",
            WorkDaysMask = WorkDayFlags.Weekdays,
            WorkStartHour = 9,
            WorkEndHour = 17,
            IsOrganizationDefault = true,
            EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var sla = new SlaService(null!, null!, null!, null!, new WorkingCalendarService(new Phase3CalRepo(cal)), null!);
        var task = new WorkTask
        {
            Status = "IN_PROGRESS",
            DepartmentId = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ResolutionTargetAt = DateTime.UtcNow.AddDays(-1)
        };
        Assert.Equal("BREACHED", sla.DetermineBreachState(task, 0));
    }

    private sealed class Phase3CalRepo : IWorkCalendarRepository
    {
        private readonly WorkCalendar _cal;
        public Phase3CalRepo(WorkCalendar cal) => _cal = cal;
        public Task<WorkCalendar> CreateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task UpdateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task<WorkCalendar?> GetByIdAsync(int id) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<WorkCalendar?> GetOrganizationDefaultAsync(DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<WorkCalendar?> GetForDepartmentAsync(int departmentId, DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<IReadOnlyList<WorkCalendar>> ListAsync() => Task.FromResult<IReadOnlyList<WorkCalendar>>(new[] { _cal });
        public Task<CalendarHoliday> AddHolidayAsync(CalendarHoliday holiday) => throw new NotImplementedException();
        public Task<bool> RemoveHolidayAsync(int holidayId) => throw new NotImplementedException();
        public Task SetDepartmentCalendarAsync(int departmentId, int? calendarId) => throw new NotImplementedException();
        public Task SeedDefaultIfEmptyAsync() => Task.CompletedTask;
    }
}

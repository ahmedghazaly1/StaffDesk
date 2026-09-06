using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class CapacityService : ICapacityService
{
    private readonly IEmployeeRepository _employees;
    private readonly IDepartmentRepository _departments;
    private readonly ILeaveRepository _leave;
    private readonly IWorkingCalendarService _calendar;
    private readonly ITaskRepository _tasks;

    public CapacityService(
        IEmployeeRepository employees,
        IDepartmentRepository departments,
        ILeaveRepository leave,
        IWorkingCalendarService calendar,
        ITaskRepository tasks)
    {
        _employees = employees;
        _departments = departments;
        _leave = leave;
        _calendar = calendar;
        _tasks = tasks;
    }

    public async Task<object> GetTeamAvailabilityAsync(
        int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole)
    {
        if (to < from)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "to must be on or after from", 400);

        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);

        var dept = await _departments.GetByIdAsync(departmentId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Department not found", 404);

        var members = (await _employees.GetByDepartmentIdAsync(departmentId))
            .Where(e => e.IsActive && !e.IsErased)
            .ToList();

        var cal = await _calendar.ResolveCalendarAsync(departmentId);
        var holidays = cal.Holidays?.ToList() ?? new List<CalendarHoliday>();
        var leave = await _leave.GetApprovedInRangeForEmployeesAsync(members.Select(m => m.Id), from, to);

        var rows = new List<object>();
        foreach (var m in members)
        {
            var memberLeave = leave.Where(l => l.EmployeeId == m.Id).ToList();
            var available = CountAvailableWorkingMinutes(from, to, cal, holidays, memberLeave);
            rows.Add(new
            {
                employeeId = m.Id,
                fullName = m.FullName,
                availableWorkingMinutes = available,
                availableWorkingHours = Math.Round(available / 60.0, 2),
                leaveDays = CountLeaveOverlapDays(from, to, memberLeave)
            });
        }

        return new
        {
            departmentId,
            departmentName = dept.Name,
            from,
            to,
            calendarId = cal.Id,
            calendarName = cal.Name,
            members = rows,
            dataAsOf = DateTime.UtcNow
        };
    }

    public async Task<object> GetWorkloadAsync(
        int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole, double overheadPercent)
    {
        if (to < from)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "to must be on or after from", 400);
        if (overheadPercent < 0 || overheadPercent > 90)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "overheadPercent must be between 0 and 90", 400);

        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);

        var members = (await _employees.GetByDepartmentIdAsync(departmentId))
            .Where(e => e.IsActive && !e.IsErased).ToList();

        var cal = await _calendar.ResolveCalendarAsync(departmentId);
        var holidays = cal.Holidays?.ToList() ?? new List<CalendarHoliday>();
        var leave = await _leave.GetApprovedInRangeForEmployeesAsync(members.Select(m => m.Id), from, to);

        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = new List<object>();
        foreach (var m in members)
        {
            var memberLeave = leave.Where(l => l.EmployeeId == m.Id).ToList();
            var availableMinutes = CountAvailableWorkingMinutes(from, to, cal, holidays, memberLeave);
            var capacityMinutes = (long)(availableMinutes * (1.0 - overheadPercent / 100.0));

            // CP-11: committed load = remaining estimates on active tasks due in period (or undated)
            var tasks = await _tasks.GetFilteredAsync(
                viewerId: 0,
                viewerDepartmentId: null,
                isAdmin: true,
                page: 1,
                limit: 500,
                status: "OPEN,IN_PROGRESS,BLOCKED,IN_REVIEW",
                assigneeId: m.Id,
                includeArchived: false);

            long committed = 0;
            var unestimated = 0;
            foreach (var t in tasks.Items)
            {
                if (t.DueAt.HasValue && (t.DueAt < fromUtc || t.DueAt >= toUtcExclusive))
                    continue;

                if (!t.EstimateMinutes.HasValue || t.EstimateMinutes.Value <= 0)
                {
                    unestimated++;
                    continue;
                }

                var remaining = Math.Max(0, t.EstimateMinutes.Value - t.LoggedMinutes);
                committed += remaining;
            }

            double? utilisation = capacityMinutes > 0
                ? Math.Round(100.0 * committed / capacityMinutes, 1)
                : null;

            rows.Add(new
            {
                employeeId = m.Id,
                fullName = m.FullName,
                availableWorkingMinutes = availableMinutes,
                capacityMinutes,
                committedLoadMinutes = committed,
                utilisationPercent = utilisation,
                unestimatedTaskCount = unestimated,
                overheadPercent
            });
        }

        return new
        {
            departmentId,
            departmentName = (await _departments.GetByIdAsync(departmentId))?.Name,
            from,
            to,
            overheadPercent,
            members = rows,
            dataAsOf = DateTime.UtcNow
        };
    }

    private async Task EnsureCanViewDeptAsync(int departmentId, int actorEmployeeId, string actorRole)
    {
        if (actorRole is User.Roles.Admin or User.Roles.HrAdmin or User.Roles.Manager) return;
        var actor = await _employees.GetByIdAsync(actorEmployeeId);
        if (actor == null || actor.DepartmentId != departmentId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Not permitted to view this department", 403);
    }

    private long CountAvailableWorkingMinutes(
        DateOnly from, DateOnly to, WorkCalendar cal, IReadOnlyList<CalendarHoliday> holidays,
        IReadOnlyList<LeaveRequest> leave)
    {
        long dayMinutes = (cal.WorkEndHour - cal.WorkStartHour) * 60L;
        long total = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!_calendar.IsWorkingDay(d, cal, holidays)) continue;

            var dayLeave = leave.Where(l => l.StartDate <= d && l.EndDate >= d).ToList();
            if (dayLeave.Count == 0)
            {
                total += dayMinutes;
                continue;
            }

            // Full-day leave zeroes the day; partial-day leave deducts half (CP-5).
            if (dayLeave.Any(l => !l.IsPartialDay))
                continue;

            total += dayMinutes / 2;
        }
        return total;
    }

    private static double CountLeaveOverlapDays(DateOnly from, DateOnly to, IReadOnlyList<LeaveRequest> leave)
    {
        double days = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var dayLeave = leave.Where(l => l.StartDate <= d && l.EndDate >= d).ToList();
            if (dayLeave.Count == 0) continue;
            days += dayLeave.Any(l => !l.IsPartialDay) ? 1.0 : 0.5;
        }
        return days;
    }
}

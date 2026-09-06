using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class TimesheetService : ITimesheetService
{
    private readonly ITimesheetRepository _timesheets;
    private readonly IEmployeeRepository _employees;
    private readonly IUserRepository _users;
    private readonly IAuditService _audit;

    public TimesheetService(
        ITimesheetRepository timesheets,
        IEmployeeRepository employees,
        IUserRepository users,
        IAuditService audit)
    {
        _timesheets = timesheets;
        _employees = employees;
        _users = users;
        _audit = audit;
    }

    public async Task<object> GetWeekAsync(int employeeId, DateOnly weekStart, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewAsync(employeeId, actorEmployeeId, actorRole);
        var sheet = await _timesheets.GetOrCreateOpenWeekAsync(employeeId, weekStart);
        return MapSheet(sheet);
    }

    public async Task EnsureTimeEntryEditableAsync(int employeeId, DateOnly workedOn)
    {
        var weekStart = TimesheetRepositoryMonday(workedOn);
        // Look up without creating — only block if an APPROVED sheet already exists
        var sheets = await _timesheets.GetForEmployeeAsync(employeeId);
        var sheet = sheets.FirstOrDefault(s => s.WeekStart == weekStart);
        if (sheet != null && sheet.State is TimesheetStates.Approved or TimesheetStates.Submitted)
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "Cannot edit time entries on a SUBMITTED or APPROVED timesheet (CP-15).", 409);
    }

    public async Task LinkEntryToWeekAsync(int employeeId, int timeEntryId, DateOnly workedOn)
    {
        await EnsureTimeEntryEditableAsync(employeeId, workedOn);
        var sheet = await _timesheets.GetOrCreateOpenWeekAsync(employeeId, workedOn);
        if (sheet.State is TimesheetStates.Approved)
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "Cannot add time to an APPROVED timesheet", 409);
        if (sheet.State == TimesheetStates.Submitted)
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "Cannot add time to a SUBMITTED timesheet until it is returned or reopened", 409);

        await _timesheets.AttachTimeEntryAsync(timeEntryId, sheet.Id);
    }

    public async Task<Timesheet> SubmitWeekAsync(int employeeId, DateOnly weekStart)
    {
        var sheet = await _timesheets.GetOrCreateOpenWeekAsync(employeeId, weekStart);

        if (sheet.EmployeeId != employeeId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You can only submit your own timesheet", 403);

        if (sheet.State is not (TimesheetStates.Open or TimesheetStates.Returned))
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                $"Cannot submit timesheet in state {sheet.State}", 400);

        sheet.State = TimesheetStates.Submitted;
        sheet.SubmittedAt = DateTime.UtcNow;
        sheet.ReviewedById = null;
        sheet.ReviewedAt = null;
        sheet.ReviewNote = null;
        await _timesheets.UpdateAsync(sheet);

        await _audit.LogAsync("TIMESHEET_SUBMITTED", employeeId, $"employee:{employeeId}",
            "SUCCESS", "Timesheet", sheet.Id.ToString(),
            changes: new { sheet.WeekStart });

        return sheet;
    }

    public async Task<Timesheet> ReviewAsync(int timesheetId, int reviewerEmployeeId, bool approve, string? note)
    {
        var sheet = await _timesheets.GetByIdAsync(timesheetId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Timesheet not found", 404);

        if (sheet.State != TimesheetStates.Submitted)
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "Only SUBMITTED timesheets can be approved or returned", 400);

        if (sheet.EmployeeId == reviewerEmployeeId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You cannot review your own timesheet", 403);

        var actorUser = await _users.GetByEmployeeIdAsync(reviewerEmployeeId);
        var isAdmin = actorUser?.Role is User.Roles.Admin or User.Roles.HrAdmin;
        var subject = await _employees.GetByIdAsync(sheet.EmployeeId);

        if (!isAdmin && subject?.ManagerId != reviewerEmployeeId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden,
                "Only the employee's manager, Admin, or HR_ADMIN may review timesheets", 403);

        sheet.State = approve ? TimesheetStates.Approved : TimesheetStates.Returned;
        sheet.ReviewedById = reviewerEmployeeId;
        sheet.ReviewedAt = DateTime.UtcNow;
        sheet.ReviewNote = note?.Trim();
        await _timesheets.UpdateAsync(sheet);

        await _audit.LogAsync(
            approve ? "TIMESHEET_APPROVED" : "TIMESHEET_RETURNED",
            reviewerEmployeeId, $"employee:{reviewerEmployeeId}", "SUCCESS",
            "Timesheet", sheet.Id.ToString(),
            changes: new { sheet.EmployeeId, sheet.WeekStart });

        return sheet;
    }

    public async Task<Timesheet> ReopenAsync(int timesheetId, int reviewerEmployeeId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Reopen reason is required", 400);

        var sheet = await _timesheets.GetByIdAsync(timesheetId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Timesheet not found", 404);

        if (sheet.State != TimesheetStates.Approved)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Only APPROVED timesheets can be reopened", 400);

        var actorUser = await _users.GetByEmployeeIdAsync(reviewerEmployeeId);
        var isAdmin = actorUser?.Role is User.Roles.Admin or User.Roles.HrAdmin;
        var subject = await _employees.GetByIdAsync(sheet.EmployeeId);

        if (!isAdmin && subject?.ManagerId != reviewerEmployeeId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Not permitted to reopen this timesheet", 403);

        // CP-15: reopen unlocks editing (RETURNED-like editable state)
        sheet.State = TimesheetStates.Returned;
        sheet.ReviewNote = reason.Trim();
        sheet.ReviewedById = reviewerEmployeeId;
        sheet.ReviewedAt = DateTime.UtcNow;
        await _timesheets.UpdateAsync(sheet);

        await _audit.LogAsync("TIMESHEET_REOPENED", reviewerEmployeeId, $"employee:{reviewerEmployeeId}",
            "SUCCESS", "Timesheet", sheet.Id.ToString(),
            changes: new { reason });

        return sheet;
    }

    public async Task<IReadOnlyList<object>> MineAsync(int employeeId)
    {
        var sheets = await _timesheets.GetForEmployeeAsync(employeeId);
        return sheets.Select(MapSheet).ToList();
    }

    public async Task<IReadOnlyList<object>> PendingReviewAsync(int managerEmployeeId)
    {
        var reports = await _employees.GetDirectReportsAsync(managerEmployeeId);
        var sheets = await _timesheets.GetSubmittedForReviewerAsync(reports.Select(r => r.Id));
        return sheets.Select(MapSheet).ToList();
    }

    private async Task EnsureCanViewAsync(int employeeId, int actorEmployeeId, string actorRole)
    {
        if (employeeId == actorEmployeeId) return;
        if (actorRole is User.Roles.Admin or User.Roles.HrAdmin) return;
        var subject = await _employees.GetByIdAsync(employeeId);
        if (subject?.ManagerId == actorEmployeeId) return;
        throw new TaskDomainException(TaskErrorCodes.Forbidden, "Not permitted to view this timesheet", 403);
    }

    private static DateOnly TimesheetRepositoryMonday(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }

    private static object MapSheet(Timesheet sheet) => new
    {
        sheet.Id,
        sheet.EmployeeId,
        employeeName = sheet.Employee?.FullName,
        weekStart = sheet.WeekStart,
        weekEnd = sheet.WeekStart.AddDays(6),
        sheet.State,
        sheet.SubmittedAt,
        sheet.ReviewedById,
        sheet.ReviewedAt,
        sheet.ReviewNote,
        totalMinutes = sheet.Entries?.Sum(e => e.Minutes) ?? 0,
        entries = (sheet.Entries ?? Array.Empty<TaskTimeEntry>()).Select(e => new
        {
            e.Id,
            e.TaskId,
            e.Minutes,
            e.Note,
            e.WorkedOn,
            e.CreatedAt
        })
    };
}

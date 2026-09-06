using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leave;
    private readonly IEmployeeRepository _employees;
    private readonly IUserRepository _users;
    private readonly IAuditService _audit;

    public LeaveService(
        ILeaveRepository leave,
        IEmployeeRepository employees,
        IUserRepository users,
        IAuditService audit)
    {
        _leave = leave;
        _employees = employees;
        _users = users;
        _audit = audit;
    }

    public async Task<LeaveRequest> RequestLeaveAsync(
        int employeeId, string type, DateOnly start, DateOnly end, bool partialDay, string? note)
    {
        type = type.Trim().ToLowerInvariant();
        if (!LeaveTypes.All.Contains(type))
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "type must be one of: " + string.Join(", ", LeaveTypes.All), 400);
        if (end < start)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "endDate must be on or after startDate", 400);

        var req = await _leave.CreateAsync(new LeaveRequest
        {
            EmployeeId = employeeId,
            Type = type,
            StartDate = start,
            EndDate = end,
            IsPartialDay = partialDay,
            Note = note?.Trim(),
            State = LeaveStates.Requested,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        return req;
    }

    public async Task<LeaveRequest> DecideAsync(int leaveId, int actorEmployeeId, bool approve, string? decisionNote)
    {
        var leave = await _leave.GetByIdAsync(leaveId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Leave request not found", 404);

        if (leave.State != LeaveStates.Requested)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, $"Leave is already {leave.State}", 400);

        // CP-6: no self-approval
        if (leave.EmployeeId == actorEmployeeId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You cannot approve or reject your own leave", 403);

        var actorUser = await _users.GetByEmployeeIdAsync(actorEmployeeId);
        var isAdmin = actorUser?.Role == User.Roles.Admin || actorUser?.Role == User.Roles.HrAdmin;

        if (!isAdmin)
        {
            var subject = await _employees.GetByIdAsync(leave.EmployeeId);
            if (subject?.ManagerId != actorEmployeeId)
                throw new TaskDomainException(TaskErrorCodes.Forbidden,
                    "Only the employee's manager, Admin, or HR_ADMIN may decide leave", 403);
        }

        leave.State = approve ? LeaveStates.Approved : LeaveStates.Rejected;
        leave.DecidedById = actorEmployeeId;
        leave.DecidedAt = DateTime.UtcNow;
        leave.DecisionNote = decisionNote?.Trim();
        await _leave.UpdateAsync(leave);

        await _audit.LogAsync(
            approve ? "LEAVE_APPROVED" : "LEAVE_REJECTED",
            actorEmployeeId,
            $"employee:{actorEmployeeId}",
            "SUCCESS",
            "LeaveRequest",
            leave.Id.ToString(),
            changes: new { leave.EmployeeId, leave.Type, leave.StartDate, leave.EndDate });

        return leave;
    }

    public async Task<LeaveRequest> CancelAsync(int leaveId, int actorEmployeeId)
    {
        var leave = await _leave.GetByIdAsync(leaveId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Leave request not found", 404);

        var actorUser = await _users.GetByEmployeeIdAsync(actorEmployeeId);
        var isPrivileged = actorUser?.Role is User.Roles.Admin or User.Roles.HrAdmin;

        if (leave.EmployeeId != actorEmployeeId && !isPrivileged)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You can only cancel your own leave", 403);

        if (leave.State is LeaveStates.Rejected or LeaveStates.Cancelled)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, $"Cannot cancel leave in state {leave.State}", 400);

        leave.State = LeaveStates.Cancelled;
        leave.DecidedById = actorEmployeeId;
        leave.DecidedAt = DateTime.UtcNow;
        await _leave.UpdateAsync(leave);
        return leave;
    }

    public async Task<IReadOnlyList<object>> ListVisibleAsync(int actorEmployeeId, string actorRole, int? employeeIdFilter = null)
    {
        // CP-8: leave type visible only to the employee and HR_ADMIN; others see availability alone.
        if (employeeIdFilter.HasValue && employeeIdFilter.Value != actorEmployeeId)
        {
            await EnsureCanViewEmployeeLeaveAsync(actorEmployeeId, actorRole, employeeIdFilter.Value);
            var rows = await _leave.GetForEmployeeAsync(employeeIdFilter.Value);
            var includeType = actorRole == User.Roles.HrAdmin;
            return rows.Select(l => MapLeave(l, includeType)).ToList();
        }

        if (actorRole == User.Roles.HrAdmin)
        {
            var all = employeeIdFilter.HasValue
                ? await _leave.GetForEmployeeAsync(employeeIdFilter.Value)
                : await _leave.ListRecentAsync();
            return all.Select(l => MapLeave(l, includeType: true)).ToList();
        }

        if (actorRole == User.Roles.Admin)
        {
            var all = employeeIdFilter.HasValue
                ? await _leave.GetForEmployeeAsync(employeeIdFilter.Value)
                : await _leave.ListRecentAsync();
            return all.Select(l => MapLeave(l, includeType: false)).ToList();
        }

        // Manager / member default: own leave (with type) plus direct reports (availability only)
        var own = await _leave.GetForEmployeeAsync(actorEmployeeId);
        var result = own.Select(l => MapLeave(l, includeType: true)).ToList();

        if (actorRole == User.Roles.Manager || true)
        {
            var reports = (await _employees.GetDirectReportsAsync(actorEmployeeId)).ToList();
            if (reports.Count > 0)
            {
                var reportLeave = await _leave.GetForEmployeesAsync(reports.Select(r => r.Id));
                result.AddRange(reportLeave.Select(l => MapLeave(l, includeType: false)));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetPendingApprovalsAsync(int managerEmployeeId)
    {
        var reports = await _employees.GetDirectReportsAsync(managerEmployeeId);
        var ids = reports.Select(r => r.Id).ToList();
        var actorUser = await _users.GetByEmployeeIdAsync(managerEmployeeId);
        if (actorUser?.Role is User.Roles.Admin or User.Roles.HrAdmin)
        {
            // Privileged: pending across org (via recent + filter)
            var recent = await _leave.ListRecentAsync(1000);
            return recent.Where(l => l.State == LeaveStates.Requested).ToList();
        }
        return await _leave.GetPendingForManagerAsync(ids);
    }

    public Task<bool> IsOnApprovedLeaveAsync(int employeeId, DateOnly date) =>
        _leave.HasApprovedLeaveOnAsync(employeeId, date);

    private async Task EnsureCanViewEmployeeLeaveAsync(int actorEmployeeId, string actorRole, int employeeId)
    {
        if (actorRole is User.Roles.Admin or User.Roles.HrAdmin) return;
        if (await IsInManagementChainAsync(actorEmployeeId, employeeId)) return;
        throw new TaskDomainException(TaskErrorCodes.NotFound, "Not found", 404);
    }

    private async Task<bool> IsInManagementChainAsync(int managerId, int employeeId)
    {
        var current = await _employees.GetByIdAsync(employeeId);
        var guard = 0;
        while (current != null && guard++ < 50)
        {
            if (current.ManagerId == managerId) return true;
            if (current.ManagerId == null) break;
            current = await _employees.GetByIdAsync(current.ManagerId.Value);
        }
        return false;
    }

    private static object MapLeave(LeaveRequest l, bool includeType) => new
    {
        l.Id,
        l.EmployeeId,
        employeeName = l.Employee?.FullName,
        type = includeType ? l.Type : null,
        availabilityOnly = !includeType,
        l.StartDate,
        l.EndDate,
        l.IsPartialDay,
        note = includeType ? l.Note : null,
        l.State,
        l.DecidedById,
        l.DecidedAt,
        l.CreatedAt
    };
}

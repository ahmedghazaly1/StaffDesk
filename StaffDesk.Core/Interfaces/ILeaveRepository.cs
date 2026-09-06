using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ILeaveRepository
{
    Task<LeaveRequest> CreateAsync(LeaveRequest request);
    Task<LeaveRequest?> GetByIdAsync(int id);
    Task UpdateAsync(LeaveRequest request);
    Task<IReadOnlyList<LeaveRequest>> GetForEmployeeAsync(int employeeId);
    Task<IReadOnlyList<LeaveRequest>> GetForEmployeesAsync(IEnumerable<int> employeeIds);
    Task<IReadOnlyList<LeaveRequest>> ListRecentAsync(int take = 500);
    Task<IReadOnlyList<LeaveRequest>> GetPendingForManagerAsync(IEnumerable<int> reportEmployeeIds);
    Task<bool> HasApprovedLeaveOnAsync(int employeeId, DateOnly date);
    Task<IReadOnlyList<LeaveRequest>> GetApprovedInRangeAsync(int employeeId, DateOnly from, DateOnly to);
    Task<IReadOnlyList<LeaveRequest>> GetApprovedInRangeForEmployeesAsync(IEnumerable<int> employeeIds, DateOnly from, DateOnly to);
}

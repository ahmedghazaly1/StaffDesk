using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class LeaveRepository : ILeaveRepository
{
    private readonly AppDbContext _db;
    public LeaveRepository(AppDbContext db) => _db = db;

    public async Task<LeaveRequest> CreateAsync(LeaveRequest request)
    {
        _db.LeaveRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public Task<LeaveRequest?> GetByIdAsync(int id) =>
        _db.LeaveRequests.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == id);

    public async Task UpdateAsync(LeaveRequest request)
    {
        request.UpdatedAt = DateTime.UtcNow;
        _db.LeaveRequests.Update(request);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetForEmployeeAsync(int employeeId) =>
        await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.Employee)
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();

    public async Task<IReadOnlyList<LeaveRequest>> GetForEmployeesAsync(IEnumerable<int> employeeIds)
    {
        var ids = employeeIds.ToList();
        return await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.Employee)
            .Where(l => ids.Contains(l.EmployeeId))
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<LeaveRequest>> ListRecentAsync(int take = 500) =>
        await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.Employee)
            .OrderByDescending(l => l.StartDate)
            .Take(take)
            .ToListAsync();

    public async Task<IReadOnlyList<LeaveRequest>> GetPendingForManagerAsync(IEnumerable<int> reportEmployeeIds)
    {
        var ids = reportEmployeeIds.ToList();
        return await _db.LeaveRequests.AsNoTracking()
            .Include(l => l.Employee)
            .Where(l => l.State == LeaveStates.Requested && ids.Contains(l.EmployeeId))
            .OrderBy(l => l.StartDate)
            .ToListAsync();
    }

    public Task<bool> HasApprovedLeaveOnAsync(int employeeId, DateOnly date) =>
        _db.LeaveRequests.AnyAsync(l =>
            l.EmployeeId == employeeId
            && l.State == LeaveStates.Approved
            && l.StartDate <= date
            && l.EndDate >= date);

    public async Task<IReadOnlyList<LeaveRequest>> GetApprovedInRangeAsync(int employeeId, DateOnly from, DateOnly to) =>
        await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeId == employeeId
                        && l.State == LeaveStates.Approved
                        && l.StartDate <= to
                        && l.EndDate >= from)
            .ToListAsync();

    public async Task<IReadOnlyList<LeaveRequest>> GetApprovedInRangeForEmployeesAsync(
        IEnumerable<int> employeeIds, DateOnly from, DateOnly to)
    {
        var ids = employeeIds.ToList();
        return await _db.LeaveRequests.AsNoTracking()
            .Where(l => ids.Contains(l.EmployeeId)
                        && l.State == LeaveStates.Approved
                        && l.StartDate <= to
                        && l.EndDate >= from)
            .ToListAsync();
    }
}

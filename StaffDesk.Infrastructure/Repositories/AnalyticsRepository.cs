using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly AppDbContext _db;
    public AnalyticsRepository(AppDbContext db) => _db = db;

    public async Task<DailyMetricSnapshot> InsertSnapshotAsync(DailyMetricSnapshot snapshot)
    {
        _db.DailyMetricSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();
        return snapshot;
    }

    public async Task<int> GetLatestVersionAsync(int departmentId, DateOnly date)
    {
        var max = await _db.DailyMetricSnapshots.AsNoTracking()
            .Where(s => s.DepartmentId == departmentId && s.Date == date)
            .Select(s => (int?)s.Version)
            .MaxAsync();
        return max ?? 0;
    }

    public async Task<DailyMetricSnapshot?> GetLatestAsync(int departmentId, DateOnly date)
    {
        return await _db.DailyMetricSnapshots.AsNoTracking()
            .Where(s => s.DepartmentId == departmentId && s.Date == date)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeAsync(int departmentId, DateOnly from, DateOnly to)
    {
        var rows = await _db.DailyMetricSnapshots.AsNoTracking()
            .Where(s => s.DepartmentId == departmentId && s.Date >= from && s.Date <= to)
            .ToListAsync();

        return rows
            .GroupBy(s => s.Date)
            .Select(g => g.OrderByDescending(x => x.Version).First())
            .OrderBy(s => s.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeForDepartmentsAsync(
        IEnumerable<int> departmentIds, DateOnly from, DateOnly to)
    {
        var ids = departmentIds.ToList();
        var rows = await _db.DailyMetricSnapshots.AsNoTracking()
            .Where(s => ids.Contains(s.DepartmentId) && s.Date >= from && s.Date <= to)
            .ToListAsync();

        return rows
            .GroupBy(s => new { s.DepartmentId, s.Date })
            .Select(g => g.OrderByDescending(x => x.Version).First())
            .OrderBy(s => s.DepartmentId).ThenBy(s => s.Date)
            .ToList();
    }

    public async Task<DateTime?> GetNewestComputedAtAsync(int? departmentId = null)
    {
        var q = _db.DailyMetricSnapshots.AsNoTracking().AsQueryable();
        if (departmentId.HasValue)
            q = q.Where(s => s.DepartmentId == departmentId.Value);
        return await q.Select(s => (DateTime?)s.ComputedAt).MaxAsync();
    }

    public async Task<IReadOnlyList<WorkTask>> GetTasksForDepartmentAsync(int departmentId) =>
        await _db.Tasks.AsNoTracking()
            .Where(t => t.DepartmentId == departmentId && t.DeletedAt == null && !t.IsArchived)
            .ToListAsync();

    public async Task<IReadOnlyList<TaskStatusInterval>> GetIntervalsForTasksAsync(IEnumerable<int> taskIds)
    {
        var ids = taskIds.ToList();
        if (ids.Count == 0) return Array.Empty<TaskStatusInterval>();
        return await _db.TaskStatusIntervals.AsNoTracking()
            .Where(i => ids.Contains(i.TaskId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TaskRequest>> GetRequestsForDepartmentAsync(int departmentId) =>
        await _db.TaskRequests.AsNoTracking()
            .Where(r => r.DepartmentId == departmentId)
            .ToListAsync();

    public async Task<IReadOnlyList<ReworkEvent>> GetReworkEventsForTasksAsync(IEnumerable<int> taskIds)
    {
        var ids = taskIds.ToList();
        if (ids.Count == 0) return Array.Empty<ReworkEvent>();
        return await _db.ReworkEvents.AsNoTracking()
            .Where(e => ids.Contains(e.TaskId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TaskActivity>> GetAssignmentActivitiesForTasksAsync(IEnumerable<int> taskIds)
    {
        var ids = taskIds.ToList();
        if (ids.Count == 0) return Array.Empty<TaskActivity>();
        return await _db.TaskActivities.AsNoTracking()
            .Where(a => ids.Contains(a.TaskId) && a.Action == "ASSIGNED" && a.NewValue != null && a.NewValue != "null")
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetActiveDepartmentIdsAsync() =>
        await _db.Departments.AsNoTracking().Select(d => d.Id).ToListAsync();
}

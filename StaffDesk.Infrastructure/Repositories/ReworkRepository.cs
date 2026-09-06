using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class ReworkRepository : IReworkRepository
{
    private readonly AppDbContext _context;

    public ReworkRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ReworkEvent> CreateReworkEventAsync(ReworkEvent reworkEvent)
    {
        reworkEvent.OccurredAt = DateTime.UtcNow;
        _context.ReworkEvents.Add(reworkEvent);
        await _context.SaveChangesAsync();
        return reworkEvent;
    }

    public async Task<IEnumerable<ReworkEvent>> GetReworkEventsByTaskIdAsync(int taskId)
    {
        return await _context.ReworkEvents
            .Include(r => r.TriggeredByEmployee)
            .Where(r => r.TaskId == taskId)
            .OrderByDescending(r => r.OccurredAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReworkEvent>> GetReworkEventsAsync(
        int? departmentId = null,
        string? category = null,
        bool? isReopen = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? limit = null)
    {
        var query = _context.ReworkEvents
            .Include(r => r.Task)
                .ThenInclude(t => t.Department)
            .Include(r => r.TriggeredByEmployee)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(r => r.Task.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(r => r.Category == category);
        }

        if (isReopen.HasValue)
        {
            query = query.Where(r => r.IsReopen == isReopen.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.OccurredAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.OccurredAt <= toDate.Value);
        }

        query = query.OrderByDescending(r => r.OccurredAt);

        if (page.HasValue && limit.HasValue)
        {
            var skip = (page.Value - 1) * limit.Value;
            query = query.Skip(skip).Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<ReworkStatistics> GetReworkStatisticsAsync(
        int departmentId,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var query = _context.ReworkEvents
            .Include(r => r.Task)
            .Where(r => r.Task.DepartmentId == departmentId);

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.OccurredAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.OccurredAt <= toDate.Value);
        }

        var allReworks = await query.ToListAsync();

        var totalReworks = allReworks.Count(r => !r.IsReopen);
        var totalReopens = allReworks.Count(r => r.IsReopen);

        // Group reworks by category
        var reworksByCategory = allReworks
            .Where(r => !r.IsReopen)
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate first-pass yield
        // Get all completed tasks in the department during the period
        var tasksQuery = _context.Tasks
            .Where(t => t.DepartmentId == departmentId && t.Status == "DONE");

        if (fromDate.HasValue)
        {
            tasksQuery = tasksQuery.Where(t => t.CompletedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            tasksQuery = tasksQuery.Where(t => t.CompletedAt <= toDate.Value);
        }

        var totalTasksCompleted = await tasksQuery.CountAsync();

        // Tasks with rework or reopen
        var tasksWithIssues = allReworks
            .Select(r => r.TaskId)
            .Distinct()
            .Count();

        var firstPassYield = totalTasksCompleted > 0 
            ? (double)(totalTasksCompleted - tasksWithIssues) / totalTasksCompleted * 100 
            : 0;

        return new ReworkStatistics
        {
            TotalReworks = totalReworks,
            TotalReopens = totalReopens,
            ReworksByCategory = reworksByCategory,
            FirstPassYield = firstPassYield,
            TotalTasksCompleted = totalTasksCompleted
        };
    }
}
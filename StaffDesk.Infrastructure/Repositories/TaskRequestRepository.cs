using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class TaskRequestRepository : ITaskRequestRepository
{
    private readonly AppDbContext _context;

    public TaskRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<TaskRequest> BaseQuery()
    {
        return _context.TaskRequests
            .Include(r => r.RequestedBy)
            .Include(r => r.Department)
            .Include(r => r.CreatedTask)
            .Include(r => r.MergedIntoRequest);
    }

    public async Task<TaskRequest?> GetByIdAsync(int id)
    {
        return await BaseQuery().FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<TaskRequest> CreateAsync(TaskRequest request)
    {
        _context.TaskRequests.Add(request);
        await _context.SaveChangesAsync();

        // Reload with navigations (RequestedBy, Department) included so callers mapping straight
        // to a response DTO get populated names instead of empty strings.
        return await BaseQuery().FirstAsync(r => r.Id == request.Id);
    }

    public async Task<TaskRequest> UpdateAsync(TaskRequest request)
    {
        request.UpdatedAt = DateTime.UtcNow;
        _context.TaskRequests.Update(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<(IEnumerable<TaskRequest> Items, int TotalCount)> GetFilteredAsync(
        int viewerId,
        int? departmentId = null,
        string? status = null,
        int? page = null,
        int? limit = null,
        string? sort = "-submittedAt")
    {
        var query = BaseQuery().Where(r => r.RequestedById == viewerId);

        if (departmentId.HasValue)
            query = query.Where(r => r.DepartmentId == departmentId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            query = query.Where(r => statuses.Contains(r.Status));
        }

        query = (sort ?? "-submittedAt").TrimStart('-') == "submittedat"
            ? (sort!.StartsWith("-") ? query.OrderByDescending(r => r.SubmittedAt) : query.OrderBy(r => r.SubmittedAt))
            : query.OrderByDescending(r => r.SubmittedAt);

        var totalCount = await query.CountAsync();

        if (page.HasValue && limit.HasValue)
        {
            var skip = (page.Value - 1) * limit.Value;
            query = query.Skip(skip).Take(limit.Value);
        }

        var items = await query.ToListAsync();
        return (items, totalCount);
    }

    public async Task<List<TaskRequest>> GetTriageQueueAsync(int departmentId)
    {
        return await BaseQuery()
            .Where(r => r.DepartmentId == departmentId && (r.Status == "SUBMITTED" || r.Status == "UNDER_TRIAGE"))
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();
    }

    public async Task<List<TaskRequest>> GetDecidedForMetricsAsync(int? departmentId, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.TaskRequests
            .Where(r => r.TriageDecidedAt != null);

        if (departmentId.HasValue)
            query = query.Where(r => r.DepartmentId == departmentId.Value);
        if (fromDate.HasValue)
            query = query.Where(r => r.TriageDecidedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(r => r.TriageDecidedAt <= toDate.Value);

        return await query.ToListAsync();
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await operation();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}

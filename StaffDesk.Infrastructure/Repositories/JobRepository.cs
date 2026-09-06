using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly AppDbContext _context;

    public JobRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Job> EnqueueAsync(string type, string payloadJson)
    {
        var job = new Job
        {
            Type = type,
            PayloadJson = payloadJson,
            State = "QUEUED",
            AttemptCount = 0,
            NextAttemptAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<Job?> ClaimNextAsync()
    {
        var now = DateTime.UtcNow;

        var jobId = await _context.Jobs
            .Where(j => j.State == "QUEUED" && j.NextAttemptAt <= now)
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.Id)
            .FirstOrDefaultAsync();

        if (jobId == 0) return null;

        var updated = await _context.Jobs
            .Where(j => j.Id == jobId && j.State == "QUEUED")
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.State, "RUNNING")
                .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
                .SetProperty(j => j.UpdatedAt, now));

        if (updated == 0) return null;

        return await _context.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId);
    }

    public async Task MarkSucceededAsync(long jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return;
        job.State = "SUCCEEDED";
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task MarkFailedAsync(long jobId, string error, int maxAttempts = 5)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return;

        job.LastError = error;
        job.UpdatedAt = DateTime.UtcNow;

        if (job.AttemptCount >= maxAttempts)
        {
            job.State = "DEAD";
        }
        else
        {
            job.State = "QUEUED";
            var delayMinutes = Math.Pow(2, job.AttemptCount);
            job.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Job?> GetByIdAsync(long id)
    {
        return await _context.Jobs.FindAsync(id);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStateAsync()
    {
        return await _context.Jobs
            .GroupBy(j => j.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count);
    }

    public async Task<double?> GetOldestQueuedAgeSecondsAsync()
    {
        var oldest = await _context.Jobs
            .Where(j => j.State == "QUEUED")
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.CreatedAt)
            .FirstOrDefaultAsync();
        if (oldest == default) return null;
        return (DateTime.UtcNow - oldest).TotalSeconds;
    }

    public async Task<IReadOnlyList<Job>> ListByStateAsync(string? state, int limit = 50)
    {
        var q = _context.Jobs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(state))
            q = q.Where(j => j.State == state);
        return await q.OrderByDescending(j => j.UpdatedAt).Take(limit).ToListAsync();
    }

    public async Task<(IReadOnlyList<Job> Items, int Total)> ListPagedAsync(string? state, int page, int limit)
    {
        var q = _context.Jobs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(state))
            q = q.Where(j => j.State == state);
        var total = await q.CountAsync();
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);
        var items = await q.OrderByDescending(j => j.UpdatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();
        return (items, total);
    }

    public async Task<bool> RequeueAsync(long jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null || job.State != "DEAD") return false;
        job.State = "QUEUED";
        job.AttemptCount = 0;
        job.LastError = null;
        job.NextAttemptAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }
}

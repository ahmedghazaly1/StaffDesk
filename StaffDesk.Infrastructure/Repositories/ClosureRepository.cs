using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class ClosureRepository : IClosureRepository
{
    private readonly AppDbContext _context;

    public ClosureRepository(AppDbContext context)
    {
        _context = context;
    }

    // ============================================
    // Outcome
    // ============================================

    public async Task<TaskOutcome> CreateOutcomeAsync(TaskOutcome outcome)
    {
        outcome.OccurredAt = DateTime.UtcNow;
        _context.TaskOutcomes.Add(outcome);
        await _context.SaveChangesAsync();
        return outcome;
    }

    public async Task<TaskOutcome?> GetOutcomeByTaskIdAsync(int taskId)
    {
        return await _context.TaskOutcomes
            .Where(o => o.TaskId == taskId)
            .OrderByDescending(o => o.OccurredAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TaskOutcome>> GetOutcomesByTaskIdAsync(int taskId)
    {
        return await _context.TaskOutcomes
            .Where(o => o.TaskId == taskId)
            .OrderByDescending(o => o.OccurredAt)
            .ToListAsync();
    }

    // ============================================
    // Closure
    // ============================================

    public async Task<TaskClosure> CreateClosureAsync(TaskClosure closure)
    {
        closure.CreatedAt = DateTime.UtcNow;
        _context.TaskClosures.Add(closure);
        await _context.SaveChangesAsync();
        return closure;
    }

    public async Task<TaskClosure?> GetClosureByTaskIdAsync(int taskId)
    {
        return await _context.TaskClosures
            .FirstOrDefaultAsync(c => c.TaskId == taskId);
    }

    public async Task<bool> HasClosureAsync(int taskId)
    {
        return await _context.TaskClosures
            .AnyAsync(c => c.TaskId == taskId);
    }
}
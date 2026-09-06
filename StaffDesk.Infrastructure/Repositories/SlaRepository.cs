using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class SlaRepository : ISlaRepository
{
    private readonly AppDbContext _context;

    public SlaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SlaPolicy?> GetPolicyAsync(int departmentId, string priority)
    {
        return await _context.SlaPolicies
            .FirstOrDefaultAsync(p => p.DepartmentId == departmentId && p.Priority == priority);
    }

    public async Task<IEnumerable<SlaPolicy>> GetPoliciesForDepartmentAsync(int departmentId)
    {
        return await _context.SlaPolicies
            .Where(p => p.DepartmentId == departmentId)
            .ToListAsync();
    }

    public async Task<SlaPolicy> SavePolicyAsync(SlaPolicy policy)
    {
        policy.UpdatedAt = DateTime.UtcNow;
        if (policy.Id == 0)
        {
            policy.CreatedAt = DateTime.UtcNow;
            _context.SlaPolicies.Add(policy);
        }
        else
        {
            _context.SlaPolicies.Update(policy);
        }
        await _context.SaveChangesAsync();

        // Reload with Department included so callers mapping straight to a response DTO get a
        // populated departmentName instead of an empty string (the freshly-inserted/tracked
        // instance never had the navigation loaded).
        return await _context.SlaPolicies
            .Include(p => p.Department)
            .FirstAsync(p => p.Id == policy.Id);
    }

    public async Task<bool> DeletePolicyAsync(int policyId)
    {
        var policy = await _context.SlaPolicies.FindAsync(policyId);
        if (policy == null) return false;
        _context.SlaPolicies.Remove(policy);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<SlaPolicy>> GetAllPoliciesAsync()
    {
        return await _context.SlaPolicies
            .Include(p => p.Department)
            .ToListAsync();
    }

    public async Task<SlaEscalation> AddEscalationAsync(SlaEscalation escalation)
    {
        escalation.NotifiedAt = DateTime.UtcNow;
        _context.SlaEscalations.Add(escalation);
        await _context.SaveChangesAsync();
        return escalation;
    }

    public async Task<bool> HasEscalationBeenSentAsync(int taskId, int level)
    {
        return await _context.SlaEscalations
            .AnyAsync(e => e.TaskId == taskId && e.Level == level);
    }

    public async Task<IEnumerable<SlaEscalation>> GetEscalationsForTaskAsync(int taskId)
    {
        return await _context.SlaEscalations
            .Where(e => e.TaskId == taskId)
            .OrderBy(e => e.NotifiedAt)
            .ToListAsync();
    }

    public async Task<SlaPolicy?> GetPolicyByIdAsync(int id)
{
    return await _context.SlaPolicies
        .Include(p => p.Department)
        .FirstOrDefaultAsync(p => p.Id == id);
}

    public async Task<TaskSlaRecord> AppendSlaRecordAsync(TaskSlaRecord record)
    {
        record.CreatedAt = DateTime.UtcNow;
        _context.TaskSlaRecords.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task<TaskSlaRecord?> GetLatestSlaRecordAsync(int taskId)
    {
        return await _context.TaskSlaRecords
            .Where(r => r.TaskId == taskId)
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TaskSlaRecord>> GetSlaHistoryAsync(int taskId)
    {
        return await _context.TaskSlaRecords
            .Where(r => r.TaskId == taskId)
            .OrderByDescending(r => r.EffectiveFrom)
            .ToListAsync();
    }
}
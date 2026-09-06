using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class ApprovalRepository : IApprovalRepository
{
    private readonly AppDbContext _context;

    public ApprovalRepository(AppDbContext context)
    {
        _context = context;
    }

    // ============================================
    // TaskApproval CRUD
    // ============================================

    public async Task<TaskApproval> CreateApprovalAsync(TaskApproval approval)
    {
        approval.CreatedAt = DateTime.UtcNow;
        _context.TaskApprovals.Add(approval);
        await _context.SaveChangesAsync();
        return approval;
    }

    public async Task<TaskApproval?> GetApprovalByIdAsync(int id)
    {
        return await _context.TaskApprovals
            .Include(a => a.Steps)
                .ThenInclude(s => s.Approver)
            .Include(a => a.Task)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<TaskApproval?> GetApprovalByTaskIdAsync(int taskId)
    {
        return await _context.TaskApprovals
            .Include(a => a.Steps)
                .ThenInclude(s => s.Approver)
            .Include(a => a.Task)
            .FirstOrDefaultAsync(a => a.TaskId == taskId);
    }

    public async Task<TaskApproval> UpdateApprovalAsync(TaskApproval approval)
    {
        _context.TaskApprovals.Update(approval);
        await _context.SaveChangesAsync();
        return approval;
    }

    public async Task<bool> DeleteApprovalAsync(int id)
    {
        var approval = await _context.TaskApprovals.FindAsync(id);
        if (approval == null) return false;
        _context.TaskApprovals.Remove(approval);
        await _context.SaveChangesAsync();
        return true;
    }

    // ============================================
    // Approval Steps
    // ============================================

    public async Task<ApprovalStep> AddStepAsync(ApprovalStep step)
    {
        step.CreatedAt = DateTime.UtcNow;
        _context.ApprovalSteps.Add(step);
        await _context.SaveChangesAsync();
        return step;
    }

    public async Task<ApprovalStep?> GetStepByIdAsync(int id)
    {
        return await _context.ApprovalSteps
            .Include(s => s.Approver)
            .Include(s => s.Task)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<ApprovalStep> UpdateStepAsync(ApprovalStep step)
    {
        _context.ApprovalSteps.Update(step);
        await _context.SaveChangesAsync();
        return step;
    }

    public async Task<bool> DeleteStepAsync(int id)
    {
        var step = await _context.ApprovalSteps.FindAsync(id);
        if (step == null) return false;
        _context.ApprovalSteps.Remove(step);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ApprovalStep>> GetStepsByTaskIdAsync(int taskId)
    {
        return await _context.ApprovalSteps
            .Include(s => s.Approver)
            .Where(s => s.TaskId == taskId)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<IEnumerable<ApprovalStep>> GetPendingStepsForApproverAsync(int approverId)
    {
        return await _context.ApprovalSteps
            .Include(s => s.Task)
            .Where(s => s.ApproverId == approverId && s.State == "PENDING")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<ApprovalStep?> GetNextPendingStepAsync(int taskId)
    {
        return await _context.ApprovalSteps
            .Include(s => s.Approver)
            .Where(s => s.TaskId == taskId && s.State == "PENDING")
            .OrderBy(s => s.Order)
            .FirstOrDefaultAsync();
    }
}
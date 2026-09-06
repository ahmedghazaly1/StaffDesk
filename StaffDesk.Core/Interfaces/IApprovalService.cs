using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IApprovalService
{
    // Create approval for a task
    Task<TaskApproval> CreateApprovalAsync(int taskId, string name, string? description, bool isRequired, List<(int? ApproverId, string? Role, int Order)> steps, int createdBy);

    // Get approval by task ID
    Task<TaskApproval?> GetApprovalByTaskIdAsync(int taskId);

    // Get approval by ID
    Task<TaskApproval?> GetApprovalByIdAsync(int id);

    // Approve or reject a step
    Task<ApprovalStep> ReviewStepAsync(int stepId, int reviewerId, string state, string? decisionNote);

    // Check if a task is fully approved
    Task<bool> IsFullyApprovedAsync(int taskId);

    // Get pending approvals for a user
    Task<IEnumerable<ApprovalStep>> GetPendingApprovalsForUserAsync(int userId);

    // Update task approval status
    Task UpdateTaskApprovalStatusAsync(int taskId);

    // Skip a step (Admin/Manager only)
    Task<ApprovalStep> SkipStepAsync(int stepId, int userId, string? reason);

    // AP-6: Reassign a pending step to another approver (Admin/Manager only)
    Task<ApprovalStep> ReassignStepAsync(int stepId, int newApproverId, int userId, string? reason);

    // Delete approval
    Task<bool> DeleteApprovalAsync(int approvalId, int userId);
}
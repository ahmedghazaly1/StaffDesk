using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IApprovalRepository
{
    // TaskApproval CRUD
    Task<TaskApproval> CreateApprovalAsync(TaskApproval approval);
    Task<TaskApproval?> GetApprovalByIdAsync(int id);
    Task<TaskApproval?> GetApprovalByTaskIdAsync(int taskId);
    Task<TaskApproval> UpdateApprovalAsync(TaskApproval approval);
    Task<bool> DeleteApprovalAsync(int id);

    // Approval Steps
    Task<ApprovalStep> AddStepAsync(ApprovalStep step);
    Task<ApprovalStep?> GetStepByIdAsync(int id);
    Task<ApprovalStep> UpdateStepAsync(ApprovalStep step);
    Task<bool> DeleteStepAsync(int id);
    Task<IEnumerable<ApprovalStep>> GetStepsByTaskIdAsync(int taskId);
    Task<IEnumerable<ApprovalStep>> GetPendingStepsForApproverAsync(int approverId);
    Task<ApprovalStep?> GetNextPendingStepAsync(int taskId);
}
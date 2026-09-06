using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ISlaRepository
{
    // Get SLA policy for a department and priority
    Task<SlaPolicy?> GetPolicyAsync(int departmentId, string priority);
    
    // Get all policies for a department
    Task<IEnumerable<SlaPolicy>> GetPoliciesForDepartmentAsync(int departmentId);
    
    // Get a policy by ID
    Task<SlaPolicy?> GetPolicyByIdAsync(int id);  // NEW
    
    // Create or update a policy
    Task<SlaPolicy> SavePolicyAsync(SlaPolicy policy);
    
    // Delete a policy
    Task<bool> DeletePolicyAsync(int policyId);
    
    // Get all policies
    Task<IEnumerable<SlaPolicy>> GetAllPoliciesAsync();
    
    // Escalation tracking
    Task<SlaEscalation> AddEscalationAsync(SlaEscalation escalation);
    Task<bool> HasEscalationBeenSentAsync(int taskId, int level);
    Task<IEnumerable<SlaEscalation>> GetEscalationsForTaskAsync(int taskId);

    Task<TaskSlaRecord> AppendSlaRecordAsync(TaskSlaRecord record);
    Task<TaskSlaRecord?> GetLatestSlaRecordAsync(int taskId);
    Task<IEnumerable<TaskSlaRecord>> GetSlaHistoryAsync(int taskId);
}
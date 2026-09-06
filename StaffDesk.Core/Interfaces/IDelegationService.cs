using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IDelegationService
{
    // Create a new delegation
    Task<Delegation> CreateDelegationAsync(
        int delegatorId,
        int delegateId,
        string scope,
        DateTime startDate,
        DateTime? endDate,
        string? reason,
        int createdBy
    );

    // Get delegation by ID
    Task<Delegation?> GetDelegationByIdAsync(int id);

    // Get all delegations for a delegator
    Task<IEnumerable<Delegation>> GetDelegationsByDelegatorAsync(int delegatorId);

    // Get all delegations for a delegate
    Task<IEnumerable<Delegation>> GetDelegationsByDelegateAsync(int delegateId);

    // Get active delegations for a delegator
    Task<IEnumerable<Delegation>> GetActiveDelegationsForDelegatorAsync(int delegatorId);

    // Get active delegations for a delegate
    Task<IEnumerable<Delegation>> GetActiveDelegationsForDelegateAsync(int delegateId);

    // Update delegation
    Task<Delegation> UpdateDelegationAsync(int id, DateTime? endDate, bool isActive, string? reason, int userId);

    // Delete delegation
    Task<bool> DeleteDelegationAsync(int id, int userId);

    // Check if a user has active delegation
    Task<bool> HasActiveDelegationAsync(int delegatorId);

    // Get delegate for a specific scope
    Task<Employee?> GetDelegateForUserAsync(int delegatorId, string scope);

    // Check if an action is delegated (WC-28)
    Task<bool> IsActionDelegatedAsync(int delegatorId, string scope);

    // Get all active delegations for a scope
    Task<IEnumerable<Delegation>> GetActiveDelegationsForScopeAsync(string scope);

    Task<bool> IsDelegateForDelegatorAsync(int delegateId, int delegatorId, string scope);

    Task<bool> IsActiveDelegateAsync(int delegateId);
}
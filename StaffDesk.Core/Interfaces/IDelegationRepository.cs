using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IDelegationRepository
{
    // CRUD
    Task<Delegation> CreateAsync(Delegation delegation);
    Task<Delegation?> GetByIdAsync(int id);
    Task<IEnumerable<Delegation>> GetByDelegatorAsync(int delegatorId);
    Task<IEnumerable<Delegation>> GetByDelegateAsync(int delegateId);
    Task<IEnumerable<Delegation>> GetActiveForDelegatorAsync(int delegatorId);
    Task<IEnumerable<Delegation>> GetActiveForDelegateAsync(int delegateId);
    Task<Delegation> UpdateAsync(Delegation delegation);
    Task<bool> DeleteAsync(int id);

    // Check if a user has active delegation
    Task<bool> HasActiveDelegationAsync(int delegatorId);
    Task<Delegation?> GetActiveDelegationForScopeAsync(int delegatorId, string scope);
    Task<IEnumerable<Delegation>> GetActiveDelegationsForScopeAsync(string scope);
    Task<Employee?> GetDelegateForUserAsync(int delegatorId, string scope);

    Task<bool> IsDelegateForDelegatorAsync(int delegateId, int delegatorId, string scope);

    // WC-29: true when employee is currently someone's active delegate.
    Task<bool> IsActiveDelegateAsync(int delegateId);
}
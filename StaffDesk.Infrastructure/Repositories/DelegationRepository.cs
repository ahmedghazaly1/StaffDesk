using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class DelegationRepository : IDelegationRepository
{
    private readonly AppDbContext _context;

    public DelegationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Delegation> CreateAsync(Delegation delegation)
    {
        delegation.CreatedAt = DateTime.UtcNow;
        delegation.UpdatedAt = DateTime.UtcNow;
        _context.Delegations.Add(delegation);
        await _context.SaveChangesAsync();
        return delegation;
    }

    public async Task<Delegation?> GetByIdAsync(int id)
    {
        return await _context.Delegations
            .Include(d => d.Delegator)
            .Include(d => d.Delegate)
            .Include(d => d.CreatedByEmployee)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<IEnumerable<Delegation>> GetByDelegatorAsync(int delegatorId)
    {
        return await _context.Delegations
            .Include(d => d.Delegator)
            .Include(d => d.Delegate)
            .Where(d => d.DelegatorId == delegatorId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Delegation>> GetByDelegateAsync(int delegateId)
    {
        return await _context.Delegations
            .Include(d => d.Delegator)
            .Include(d => d.Delegate)
            .Where(d => d.DelegateId == delegateId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Delegation>> GetActiveForDelegatorAsync(int delegatorId)
    {
        var now = DateTime.UtcNow;
        return await _context.Delegations
            .Include(d => d.Delegator)
            .Include(d => d.Delegate)
            .Where(d => d.DelegatorId == delegatorId &&
                        d.IsActive &&
                        d.StartDate <= now &&
                        (!d.EndDate.HasValue || d.EndDate.Value >= now))
            .OrderBy(d => d.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Delegation>> GetActiveForDelegateAsync(int delegateId)
    {
        var now = DateTime.UtcNow;
        return await _context.Delegations
            .Include(d => d.Delegator)
            .Include(d => d.Delegate)
            .Where(d => d.DelegateId == delegateId &&
                        d.IsActive &&
                        d.StartDate <= now &&
                        (!d.EndDate.HasValue || d.EndDate.Value >= now))
            .OrderBy(d => d.StartDate)
            .ToListAsync();
    }

    public async Task<Delegation> UpdateAsync(Delegation delegation)
    {
        delegation.UpdatedAt = DateTime.UtcNow;
        _context.Delegations.Update(delegation);
        await _context.SaveChangesAsync();
        return delegation;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var delegation = await _context.Delegations.FindAsync(id);
        if (delegation == null) return false;
        _context.Delegations.Remove(delegation);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasActiveDelegationAsync(int delegatorId)
    {
        var now = DateTime.UtcNow;
        return await _context.Delegations
            .AnyAsync(d => d.DelegatorId == delegatorId &&
                           d.IsActive &&
                           d.StartDate <= now &&
                           (!d.EndDate.HasValue || d.EndDate.Value >= now));
    }

    public async Task<Delegation?> GetActiveDelegationForScopeAsync(int delegatorId, string scope)
    {
        var now = DateTime.UtcNow;
        return await _context.Delegations
            .Include(d => d.Delegate)
            .FirstOrDefaultAsync(d => d.DelegatorId == delegatorId &&
                                      d.IsActive &&
                                      d.StartDate <= now &&
                                      (!d.EndDate.HasValue || d.EndDate.Value >= now) &&
                                      (d.Scope == "ALL" || d.Scope == scope));
    }

    public async Task<IEnumerable<Delegation>> GetActiveDelegationsForScopeAsync(string scope)
    {
        var now = DateTime.UtcNow;
        return await _context.Delegations
            .Include(d => d.Delegator)
            .Include(d => d.Delegate)
            .Where(d => d.IsActive &&
                        d.StartDate <= now &&
                        (!d.EndDate.HasValue || d.EndDate.Value >= now) &&
                        (d.Scope == "ALL" || d.Scope == scope))
            .ToListAsync();
    }

    public async Task<Employee?> GetDelegateForUserAsync(int delegatorId, string scope)
    {
        var delegation = await GetActiveDelegationForScopeAsync(delegatorId, scope);
        return delegation?.Delegate;
    }

    public async Task<bool> IsDelegateForDelegatorAsync(int delegateId, int delegatorId, string scope)
    {
        var now = DateTime.UtcNow;
        return await _context.Delegations
            .AnyAsync(d => d.DelegatorId == delegatorId &&
                           d.DelegateId == delegateId &&
                           d.IsActive &&
                           d.StartDate <= now &&
                           (!d.EndDate.HasValue || d.EndDate.Value >= now) &&
                           (d.Scope == "ALL" || d.Scope == scope));
    }

    public async Task<bool> IsActiveDelegateAsync(int delegateId)
    {
        var now = DateTime.UtcNow;
        return await _context.Delegations
            .AnyAsync(d => d.DelegateId == delegateId &&
                           d.IsActive &&
                           d.StartDate <= now &&
                           (!d.EndDate.HasValue || d.EndDate.Value >= now));
    }
}
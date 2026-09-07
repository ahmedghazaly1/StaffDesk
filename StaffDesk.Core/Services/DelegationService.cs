using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class DelegationService : IDelegationService
{
    private readonly IDelegationRepository _delegationRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IAuditService _auditService;

    public DelegationService(
        IDelegationRepository delegationRepository,
        IEmployeeRepository employeeRepository,
        ITaskRepository taskRepository,
        IAuditService auditService)
    {
        _delegationRepository = delegationRepository;
        _employeeRepository = employeeRepository;
        _taskRepository = taskRepository;
        _auditService = auditService;
    }

    public async Task<Delegation> CreateDelegationAsync(
        int delegatorId,
        int delegateId,
        string scope,
        DateTime startDate,
        DateTime? endDate,
        string? reason,
        int createdBy)
    {
        // Validate
        if (delegatorId == delegateId)
            throw new ArgumentException("An employee cannot delegate to themselves");

        var validScopes = new[] { "ALL", "APPROVALS", "TASKS" };
        if (!validScopes.Contains(scope))
            throw new ArgumentException($"Scope must be one of: {string.Join(", ", validScopes)}");

        var delegator = await _employeeRepository.GetByIdAsync(delegatorId);
        if (delegator == null)
            throw new ArgumentException("Delegator not found");
        if (!delegator.IsActive)
            throw new ArgumentException("Delegator is not active");

        var delegateEmp = await _employeeRepository.GetByIdAsync(delegateId);
        if (delegateEmp == null)
            throw new ArgumentException("Delegate not found");
        if (!delegateEmp.IsActive)
            throw new ArgumentException("Delegate is not active");

        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("End date must be after start date");

        var startUtc = startDate.Kind == DateTimeKind.Utc ? startDate : startDate.ToUniversalTime();
        DateTime? endUtc = endDate.HasValue
            ? (endDate.Value.Kind == DateTimeKind.Utc ? endDate.Value : endDate.Value.ToUniversalTime())
            : null;

        await EnsureNoOverlappingDelegationAsync(delegatorId, scope, startUtc, endUtc, excludeId: null);

        var delegation = new Delegation
        {
            DelegatorId = delegatorId,
            DelegateId = delegateId,
            Scope = scope,
            StartDate = startUtc,
            EndDate = endUtc,
            Reason = reason,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _delegationRepository.CreateAsync(delegation);

        await _auditService.LogAsync(
            eventType: "DELEGATION_CREATED",
            actorId: createdBy,
            actorLabel: $"{delegator.FullName} ({delegator.JobTitle})",
            outcome: "SUCCESS",
            targetType: "Delegation",
            targetId: created.Id.ToString(),
            changes: new
            {
                delegateId = delegateEmp.Id,
                delegateName = delegateEmp.FullName,
                scope,
                startDate = startUtc,
                endDate = endUtc,
                reason
            }
        );

        return created;
    }

    public async Task<Delegation?> GetDelegationByIdAsync(int id)
    {
        return await _delegationRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Delegation>> GetDelegationsByDelegatorAsync(int delegatorId)
    {
        return await _delegationRepository.GetByDelegatorAsync(delegatorId);
    }

    public async Task<IEnumerable<Delegation>> GetDelegationsByDelegateAsync(int delegateId)
    {
        return await _delegationRepository.GetByDelegateAsync(delegateId);
    }

    public async Task<IEnumerable<Delegation>> GetActiveDelegationsForDelegatorAsync(int delegatorId)
    {
        return await _delegationRepository.GetActiveForDelegatorAsync(delegatorId);
    }

    public async Task<IEnumerable<Delegation>> GetActiveDelegationsForDelegateAsync(int delegateId)
    {
        return await _delegationRepository.GetActiveForDelegateAsync(delegateId);
    }

    public async Task<Delegation> UpdateDelegationAsync(int id, DateTime? endDate, bool isActive, string? reason, int userId)
    {
        var delegation = await _delegationRepository.GetByIdAsync(id);
        if (delegation == null)
            throw new ArgumentException("Delegation not found");

        DateTime? endUtc = endDate.HasValue
            ? (endDate.Value.Kind == DateTimeKind.Utc ? endDate.Value : endDate.Value.ToUniversalTime())
            : null;
        if (endUtc.HasValue && endUtc.Value < delegation.StartDate)
            throw new ArgumentException("End date must be after start date");

        if (isActive)
        {
            await EnsureNoOverlappingDelegationAsync(
                delegation.DelegatorId, delegation.Scope, delegation.StartDate, endUtc, excludeId: id);
        }

        delegation.EndDate = endUtc;
        delegation.IsActive = isActive;
        delegation.Reason = reason ?? delegation.Reason;
        delegation.UpdatedAt = DateTime.UtcNow;

        var updated = await _delegationRepository.UpdateAsync(delegation);

        await _auditService.LogAsync(
            eventType: "DELEGATION_UPDATED",
            actorId: userId,
            actorLabel: $"User {userId}",
            outcome: "SUCCESS",
            targetType: "Delegation",
            targetId: delegation.Id.ToString(),
            changes: new { endDate = endUtc, isActive, reason }
        );
        return updated;
    }

    public async Task<bool> DeleteDelegationAsync(int id, int userId)
    {
        var delegation = await _delegationRepository.GetByIdAsync(id);
        if (delegation == null) return false;

        await _auditService.LogAsync(
            eventType: "DELEGATION_DELETED",
            actorId: userId,
            actorLabel: $"User {userId}",
            outcome: "SUCCESS",
            targetType: "Delegation",
            targetId: delegation.Id.ToString(),
            changes: new
            {
                delegatorId = delegation.DelegatorId,
                delegateId = delegation.DelegateId,
                scope = delegation.Scope,
                reason = delegation.Reason
            }
        );

        return await _delegationRepository.DeleteAsync(id);
    }

    public async Task<bool> HasActiveDelegationAsync(int delegatorId)
    {
        return await _delegationRepository.HasActiveDelegationAsync(delegatorId);
    }

    public async Task<Employee?> GetDelegateForUserAsync(int delegatorId, string scope)
    {
        return await _delegationRepository.GetDelegateForUserAsync(delegatorId, scope);
    }

    public async Task<bool> IsActionDelegatedAsync(int delegatorId, string scope)
    {
        var delegateEmp = await GetDelegateForUserAsync(delegatorId, scope);
        return delegateEmp != null;
    }

    public async Task<IEnumerable<Delegation>> GetActiveDelegationsForScopeAsync(string scope)
    {
        return await _delegationRepository.GetActiveDelegationsForScopeAsync(scope);
    }

    public Task<bool> IsDelegateForDelegatorAsync(int delegateId, int delegatorId, string scope)
    {
        return _delegationRepository.IsDelegateForDelegatorAsync(delegateId, delegatorId, scope);
    }

    public Task<bool> IsActiveDelegateAsync(int delegateId)
    {
        return _delegationRepository.IsActiveDelegateAsync(delegateId);
    }

    private async Task EnsureNoOverlappingDelegationAsync(
        int delegatorId, string scope, DateTime startUtc, DateTime? endUtc, int? excludeId)
    {
        var existing = await _delegationRepository.GetByDelegatorAsync(delegatorId);
        var conflict = existing.FirstOrDefault(d =>
            d.IsActive
            && (!excludeId.HasValue || d.Id != excludeId.Value)
            && ScopesConflict(d.Scope, scope)
            && DateRangesOverlap(d.StartDate, d.EndDate, startUtc, endUtc));

        if (conflict != null)
            throw new InvalidOperationException(
                $"This window overlaps an existing {conflict.Scope} delegation (id {conflict.Id})");
    }

    internal static bool ScopesConflict(string a, string b) =>
        a == "ALL" || b == "ALL" || a == b;

    internal static bool DateRangesOverlap(DateTime startA, DateTime? endA, DateTime startB, DateTime? endB)
    {
        var endAVal = endA ?? DateTime.MaxValue;
        var endBVal = endB ?? DateTime.MaxValue;
        return startA <= endBVal && startB <= endAVal;
    }
}
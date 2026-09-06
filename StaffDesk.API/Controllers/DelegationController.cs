using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Entities;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/delegations")]
[Authorize]
public class DelegationController : ApiControllerBase
{
    private readonly IDelegationService _delegationService;
    private readonly IUserRepository _userRepository;

    public DelegationController(
        IDelegationService delegationService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _delegationService = delegationService;
    }

    // ============================================
    // POST /v1/delegations - Create delegation (WC-28)
    // ============================================
    [HttpPost]
    public async Task<IActionResult> CreateDelegation([FromBody] DelegationCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var delegation = await _delegationService.CreateDelegationAsync(
                employeeId.Value,
                dto.DelegateId,
                dto.Scope,
                dto.StartDate,
                dto.EndDate,
                dto.Reason,
                employeeId.Value
            );

            return CreatedAtAction(nameof(GetDelegation), new { id = delegation.Id }, MapToResponse(delegation));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // ============================================
    // GET /v1/delegations - Get all delegations for current user
    // ============================================
    [HttpGet]
    public async Task<IActionResult> GetDelegations()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var delegations = await _delegationService.GetDelegationsByDelegatorAsync(employeeId.Value);
        return Ok(delegations.Select(MapToResponse));
    }

    // ============================================
    // GET /v1/delegations/{id} - Get delegation by ID
    // ============================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDelegation(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var delegation = await _delegationService.GetDelegationByIdAsync(id);
        if (delegation == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Delegation not found"));

        // Only delegator, delegate, or Admin can view
        if (delegation.DelegatorId != employeeId && 
            delegation.DelegateId != employeeId)
        {
            var userRole = await GetUserRoleAsync(employeeId.Value);
            if (userRole != "Admin")
                return Forbid();
        }

        return Ok(MapToResponse(delegation));
    }

    // ============================================
    // GET /v1/delegations/delegate - Get delegations where I'm the delegate
    // ============================================
    [HttpGet("delegate")]
    public async Task<IActionResult> GetDelegationsAsDelegate()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var delegations = await _delegationService.GetDelegationsByDelegateAsync(employeeId.Value);
        return Ok(delegations.Select(MapToResponse));
    }

    // ============================================
    // GET /v1/delegations/active - Get active delegations
    // ============================================
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveDelegations()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var delegations = await _delegationService.GetActiveDelegationsForDelegatorAsync(employeeId.Value);
        return Ok(delegations.Select(MapToResponse));
    }

    // ============================================
    // GET /v1/delegations/active/delegate - Get active delegations where I'm the delegate
    // ============================================
    [HttpGet("active/delegate")]
    public async Task<IActionResult> GetActiveDelegationsAsDelegate()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var delegations = await _delegationService.GetActiveDelegationsForDelegateAsync(employeeId.Value);
        return Ok(delegations.Select(MapToResponse));
    }

    // ============================================
    // GET /v1/delegations/check/{scope} - Check if action is delegated
    // ============================================
    [HttpGet("check/{scope}")]
    public async Task<IActionResult> CheckDelegation(string scope)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var validScopes = new[] { "ALL", "APPROVALS", "TASKS" };
        if (!validScopes.Contains(scope))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "Scope must be ALL, APPROVALS, or TASKS"));

        var isDelegated = await _delegationService.IsActionDelegatedAsync(employeeId.Value, scope);
        var delegateEmp = await _delegationService.GetDelegateForUserAsync(employeeId.Value, scope);

        return Ok(new
        {
            isDelegated,
            delegateId = delegateEmp?.Id,
            delegateName = delegateEmp?.FullName
        });
    }

    // ============================================
    // PUT /v1/delegations/{id} - Update delegation
    // ============================================
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDelegation(int id, [FromBody] DelegationUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var delegation = await _delegationService.GetDelegationByIdAsync(id);
        if (delegation == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Delegation not found"));

        // Only delegator or Admin can update
        if (delegation.DelegatorId != employeeId)
        {
            var userRole = await GetUserRoleAsync(employeeId.Value);
            if (userRole != "Admin")
                return Forbid();
        }

        try
        {
            var updated = await _delegationService.UpdateDelegationAsync(
                id,
                dto.EndDate,
                dto.IsActive,
                dto.Reason,
                employeeId.Value
            );

            return Ok(MapToResponse(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // ============================================
    // DELETE /v1/delegations/{id} - Delete delegation
    // ============================================
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDelegation(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var delegation = await _delegationService.GetDelegationByIdAsync(id);
        if (delegation == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Delegation not found"));

        // Only delegator or Admin can delete
        if (delegation.DelegatorId != employeeId)
        {
            var userRole = await GetUserRoleAsync(employeeId.Value);
            if (userRole != "Admin")
                return Forbid();
        }

        var deleted = await _delegationService.DeleteDelegationAsync(id, employeeId.Value);
        if (!deleted)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Delegation not found"));

        return NoContent();
    }

    // ============================================
    // Helper Methods
    // ============================================

    private DelegationResponseDto MapToResponse(Delegation delegation)
    {
        return new DelegationResponseDto
        {
            Id = delegation.Id,
            DelegatorId = delegation.DelegatorId,
            DelegatorName = delegation.Delegator?.FullName ?? string.Empty,
            DelegateId = delegation.DelegateId,
            DelegateName = delegation.Delegate?.FullName ?? string.Empty,
            Scope = delegation.Scope,
            StartDate = delegation.StartDate,
            EndDate = delegation.EndDate,
            IsActive = delegation.IsActive,
            Reason = delegation.Reason,
            CreatedAt = delegation.CreatedAt
        };
    }

    private async Task<string> GetUserRoleAsync(int employeeId)
    {
        var user = await _userRepository.GetByEmployeeIdAsync(employeeId);
        return user?.Role ?? "Member";
    }
}
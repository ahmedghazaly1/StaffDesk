using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Exceptions;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/governance")]
[Authorize(Roles = "Admin,HR_ADMIN")]
public class GovernanceController : ApiControllerBase
{
    private readonly IGovernanceService _governance;

    public GovernanceController(IGovernanceService governance, IUserRepository userRepository)
        : base(userRepository)
    {
        _governance = governance;
    }

    // GET /v1/governance/retention-policies
    [HttpGet("retention-policies")]
    public async Task<IActionResult> GetRetentionPolicies()
    {
        var policies = await _governance.GetRetentionPoliciesAsync();
        return Ok(policies);
    }

    // POST /v1/governance/purge-runs?dryRun=true
    [HttpPost("purge-runs")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> QueuePurge([FromQuery] bool dryRun = false)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var (jobId, purgeRunId) = await _governance.QueuePurgeAsync(employeeId.Value, dryRun);
        return Accepted(new { jobId, purgeRunId, dryRun });
    }

    // GET /v1/governance/purge-runs/{id}
    [HttpGet("purge-runs/{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPurgeRun(long id)
    {
        var run = await _governance.GetPurgeRunAsync(id);
        if (run == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Purge run not found"));
        return Ok(run);
    }

    // POST /v1/governance/legal-holds
    [HttpPost("legal-holds")]
    public async Task<IActionResult> PlaceHold([FromBody] PlaceLegalHoldRequest request)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            var hold = await _governance.PlaceLegalHoldAsync(
                request.TargetType.Trim(),
                request.TargetId.Trim(),
                request.Reason.Trim(),
                employeeId.Value);
            return StatusCode(201, hold);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiError.Build(TaskErrorCodes.DuplicateResource, ex.Message));
        }
    }

    // GET /v1/governance/legal-holds
    [HttpGet("legal-holds")]
    public async Task<IActionResult> ListHolds()
    {
        return Ok(await _governance.ListActiveHoldsAsync());
    }

    // DELETE /v1/governance/legal-holds/{id}
    [HttpDelete("legal-holds/{id:long}")]
    public async Task<IActionResult> LiftHold(long id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            await _governance.LiftLegalHoldAsync(id, employeeId.Value);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Legal hold not found"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }
}

public class PlaceLegalHoldRequest
{
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

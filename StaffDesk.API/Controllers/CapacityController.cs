using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/capacity")]
[Authorize]
public class CapacityController : ApiControllerBase
{
    private readonly ICapacityService _capacity;

    public CapacityController(ICapacityService capacity, IUserRepository userRepository)
        : base(userRepository)
    {
        _capacity = capacity;
    }

    [HttpGet("availability")]
    public async Task<IActionResult> Availability(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            var result = await _capacity.GetTeamAvailabilityAsync(
                departmentId, from, to, actorId.Value, await GetActorRoleAsync());
            return Ok(result);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("workload")]
    public async Task<IActionResult> Workload(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] double overheadPercent = 20)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            var result = await _capacity.GetWorkloadAsync(
                departmentId, from, to, actorId.Value, await GetActorRoleAsync(), overheadPercent);
            return Ok(result);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    private async Task<string> GetActorRoleAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId)) return "Member";
        var user = await UserRepository.GetByIdAsync(userId);
        return user?.Role ?? "Member";
    }
}

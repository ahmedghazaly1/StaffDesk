using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/leave")]
[Authorize]
public class LeaveController : ApiControllerBase
{
    private readonly ILeaveService _leave;

    public LeaveController(ILeaveService leave, IUserRepository userRepository)
        : base(userRepository)
    {
        _leave = leave;
    }

    [HttpPost]
    public async Task<IActionResult> RequestLeave([FromBody] LeaveRequestDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            var leave = await _leave.RequestLeaveAsync(
                employeeId.Value, dto.Type, dto.StartDate, dto.EndDate, dto.IsPartialDay, dto.Note);
            return Created($"/v1/leave/{leave.Id}", new
            {
                leave.Id, leave.EmployeeId, leave.Type, leave.StartDate, leave.EndDate,
                leave.IsPartialDay, leave.Note, leave.State, leave.CreatedAt
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? employeeId = null)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();
        var role = await GetActorRoleAsync();

        try
        {
            var rows = await _leave.ListVisibleAsync(actorId.Value, role, employeeId);
            return Ok(rows);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending()
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        var rows = await _leave.GetPendingApprovalsAsync(actorId.Value);
        return Ok(rows.Select(l => new
        {
            l.Id, l.EmployeeId, employeeName = l.Employee?.FullName,
            l.Type, l.StartDate, l.EndDate, l.IsPartialDay, l.Note, l.State, l.CreatedAt
        }));
    }

    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] LeaveDecideDto dto)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            var leave = await _leave.DecideAsync(id, actorId.Value, dto.Approve, dto.Note);
            return Ok(new
            {
                leave.Id, leave.State, leave.DecidedById, leave.DecidedAt, leave.DecisionNote
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            var leave = await _leave.CancelAsync(id, actorId.Value);
            return Ok(new { leave.Id, leave.State });
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

public class LeaveRequestDto
{
    public string Type { get; set; } = "annual";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsPartialDay { get; set; }
    public string? Note { get; set; }
}

public class LeaveDecideDto
{
    public bool Approve { get; set; }
    public string? Note { get; set; }
}

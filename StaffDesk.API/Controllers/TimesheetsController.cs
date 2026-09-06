using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/timesheets")]
[Authorize]
public class TimesheetsController : ApiControllerBase
{
    private readonly ITimesheetService _timesheets;

    public TimesheetsController(ITimesheetService timesheets, IUserRepository userRepository)
        : base(userRepository)
    {
        _timesheets = timesheets;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        return Ok(await _timesheets.MineAsync(employeeId.Value));
    }

    [HttpGet("week")]
    public async Task<IActionResult> Week([FromQuery] DateOnly weekStart, [FromQuery] int? employeeId = null)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();
        var target = employeeId ?? actorId.Value;

        try
        {
            var sheet = await _timesheets.GetWeekAsync(target, weekStart, actorId.Value, await GetActorRoleAsync());
            return Ok(sheet);
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
        return Ok(await _timesheets.PendingReviewAsync(actorId.Value));
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] TimesheetWeekDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            var sheet = await _timesheets.SubmitWeekAsync(employeeId.Value, dto.WeekStart);
            return Ok(new { sheet.Id, sheet.WeekStart, sheet.State, sheet.SubmittedAt });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] TimesheetReviewDto dto)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            var sheet = await _timesheets.ReviewAsync(id, actorId.Value, dto.Approve, dto.Note);
            return Ok(new { sheet.Id, sheet.State, sheet.ReviewedById, sheet.ReviewedAt, sheet.ReviewNote });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id, [FromBody] TimesheetReopenDto dto)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            var sheet = await _timesheets.ReopenAsync(id, actorId.Value, dto.Reason);
            return Ok(new { sheet.Id, sheet.State, sheet.ReviewNote });
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

public class TimesheetWeekDto
{
    public DateOnly WeekStart { get; set; }
}

public class TimesheetReviewDto
{
    public bool Approve { get; set; }
    public string? Note { get; set; }
}

public class TimesheetReopenDto
{
    public string Reason { get; set; } = "";
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Entities;
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

    [HttpPost("attachments")]
    [RequestSizeLimit(TimesheetAttachmentRules.MaxSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment([FromForm] TimesheetAttachmentUploadDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        if (dto.File == null || dto.File.Length == 0)
            return StatusCode(400, ApiError.Build(TaskErrorCodes.ValidationError, "Choose a file to upload"));

        try
        {
            await using var stream = dto.File.OpenReadStream();
            var attachment = await _timesheets.UploadAttachmentAsync(
                employeeId.Value, dto.WeekStart, dto.File.FileName, dto.File.ContentType, stream, dto.File.Length);
            return Ok(attachment);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("attachments/{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(int attachmentId)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            var file = await _timesheets.DownloadAttachmentAsync(attachmentId, actorId.Value, await GetActorRoleAsync());
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpDelete("attachments/{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(int attachmentId)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();

        try
        {
            await _timesheets.DeleteAttachmentAsync(attachmentId, actorId.Value);
            return NoContent();
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

public class TimesheetAttachmentUploadDto
{
    /// <summary>First day of the month the file belongs to.</summary>
    public DateOnly WeekStart { get; set; }
    public IFormFile? File { get; set; }
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

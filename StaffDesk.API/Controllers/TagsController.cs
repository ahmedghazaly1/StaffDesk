using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/tags")]
[Authorize]
public class TagsController : ApiControllerBase
{
    private readonly ITaskService _taskService;

    public TagsController(ITaskService taskService, IUserRepository userRepository)
        : base(userRepository)
    {
        _taskService = taskService;
    }

    // GET /v1/tags - list with usage counts
    [HttpGet]
    public async Task<IActionResult> GetTags()
    {
        var tagsWithCounts = await _taskService.GetTagsWithUsageCountsAsync();
        var data = tagsWithCounts.Select(x => new TagResponseDto
        {
            Id = x.Tag.Id,
            Name = x.Tag.Name,
            Color = x.Tag.Color,
            UsageCount = x.Count
        });
        return Ok(data);
    }

    // POST /v1/tags - ADMIN only (enforced in TaskService.CreateTagAsync too)
    [HttpPost]
    public async Task<IActionResult> CreateTag([FromBody] TagCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var tag = await _taskService.CreateTagAsync(dto.Name, employeeId.Value);
            return StatusCode(201, new TagResponseDto { Id = tag.Id, Name = tag.Name, Color = tag.Color, UsageCount = 0 });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // DELETE /v1/tags/{id} - ADMIN only
    [HttpDelete("{id}")]
    [AdminOnly]
    public async Task<IActionResult> DeleteTag(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _taskService.DeleteTagAsync(id, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Tag not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }
}

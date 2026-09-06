using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/checklist-items")]
[Authorize]
public class ChecklistItemsController : ApiControllerBase
{
    private readonly ITaskService _taskService;

    public ChecklistItemsController(ITaskService taskService, IUserRepository userRepository)
        : base(userRepository)
    {
        _taskService = taskService;
    }

    // PATCH /v1/checklist-items/{id}
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] ChecklistItemUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var item = await _taskService.UpdateChecklistItemAsync(id, employeeId.Value, dto.Label, dto.IsDone, dto.Position);
            return Ok(new ChecklistItemResponseDto
            {
                Id = item.Id,
                TaskId = item.TaskId,
                Label = item.Label,
                IsDone = item.IsDone,
                Position = item.Position,
                CompletedAt = item.CompletedAt,
                CompletedById = item.CompletedById,
                CompletedByName = item.CompletedBy?.FullName
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // DELETE /v1/checklist-items/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _taskService.DeleteChecklistItemAsync(id, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Checklist item not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }
}

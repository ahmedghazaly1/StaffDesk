using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/saved-views")]
[Authorize]
public class SavedViewsController : ApiControllerBase
{
    private readonly ISavedViewService _savedViewService;
    private readonly ITaskService _taskService;

    public SavedViewsController(
        ISavedViewService savedViewService,
        ITaskService taskService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _savedViewService = savedViewService;
        _taskService = taskService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavedViewCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            var view = await _savedViewService.CreateAsync(
                employeeId.Value, dto.Name, dto.Visibility, dto.DepartmentId, dto.FilterJson);
            return CreatedAtAction(nameof(GetById), new { id = view.Id }, MapToResponse(view));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var views = await _savedViewService.ListForUserAsync(employeeId.Value);
        return Ok(views.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var view = await _savedViewService.GetByIdAsync(id, employeeId.Value);
        if (view == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Saved view not found"));

        return Ok(MapToResponse(view));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SavedViewUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            var view = await _savedViewService.UpdateAsync(
                id, employeeId.Value, dto.Name, dto.Visibility, dto.DepartmentId, dto.FilterJson);
            return Ok(MapToResponse(view));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            var deleted = await _savedViewService.DeleteAsync(id, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Saved view not found"));
            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("{id}/tasks")]
    public async Task<IActionResult> ApplyView(int id, [FromQuery] int? page = 1, [FromQuery] int? limit = 25)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        try
        {
            var (items, total) = await _savedViewService.ApplyViewAsync(id, employeeId.Value, page, limit);
            var data = new List<TaskResponseDto>();
            foreach (var task in items)
                data.Add(await MapTaskAsync(task, employeeId.Value));

            return Ok(new TaskListResponseDto
            {
                Data = data,
                Page = page ?? 1,
                Limit = limit ?? 25,
                Total = total,
                TotalPages = (int)Math.Ceiling(total / (double)(limit ?? 25))
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    private static SavedViewResponseDto MapToResponse(Core.Entities.SavedView view) => new()
    {
        Id = view.Id,
        Name = view.Name,
        OwnerId = view.OwnerId,
        OwnerName = view.Owner?.FullName ?? string.Empty,
        Visibility = view.Visibility,
        DepartmentId = view.DepartmentId,
        DepartmentName = view.Department?.Name,
        FilterJson = view.FilterJson,
        CreatedAt = view.CreatedAt,
        UpdatedAt = view.UpdatedAt
    };

    private async Task<TaskResponseDto> MapTaskAsync(Core.Entities.WorkTask task, int viewerId)
    {
        var isOverdue = task.DueAt.HasValue && task.DueAt.Value < DateTime.UtcNow &&
                        task.Status != "DONE" && task.Status != "CANCELLED";

        return new TaskResponseDto
        {
            Id = task.Id,
            Key = task.Key,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DepartmentId = task.DepartmentId,
            DepartmentName = task.Department?.Name ?? string.Empty,
            AssigneeId = task.AssigneeId,
            AssigneeName = task.Assignee?.FullName,
            CreatedById = task.CreatedById,
            CreatedByName = task.CreatedBy?.FullName ?? string.Empty,
            DueAt = task.DueAt,
            IsOverdue = isOverdue,
            IsArchived = task.IsArchived,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            ReworkCount = task.ReworkCount,
            ReopenCount = task.ReopenCount,
            BreachState = task.BreachState,
            ResponseTargetAt = task.ResponseTargetAt,
            ResolutionTargetAt = task.ResolutionTargetAt
        };
    }
}

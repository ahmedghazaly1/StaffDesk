using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/rework")]
[Authorize]
public class ReworkController : ApiControllerBase
{
    private readonly IReworkRepository _reworkRepository;
    private readonly ITaskService _taskService;

    public ReworkController(
        IReworkRepository reworkRepository,
        ITaskService taskService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _reworkRepository = reworkRepository;
        _taskService = taskService;
    }

    [HttpGet("tasks/{taskId}")]
    public async Task<IActionResult> GetTaskReworkEvents(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        var events = await _reworkRepository.GetReworkEventsByTaskIdAsync(taskId);
        return Ok(events.Select(e => new
        {
            e.Id,
            e.TaskId,
            e.Category,
            e.Note,
            e.TriggeredBy,
            TriggeredByName = e.TriggeredByEmployee?.FullName,
            e.OccurredAt,
            e.IsReopen
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetReworkEvents(
        [FromQuery] int? departmentId = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isReopen = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? page = 1,
        [FromQuery] int? limit = 50)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var events = await _reworkRepository.GetReworkEventsAsync(
            departmentId, category, isReopen, fromDate, toDate, page, limit);

        return Ok(events.Select(e => new
        {
            e.Id,
            e.TaskId,
            TaskKey = e.Task?.Key,
            TaskTitle = e.Task?.Title,
            e.Category,
            e.Note,
            e.TriggeredBy,
            TriggeredByName = e.TriggeredByEmployee?.FullName,
            e.OccurredAt,
            e.IsReopen
        }));
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetReworkStatistics(
        [FromQuery] int departmentId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var stats = await _reworkRepository.GetReworkStatisticsAsync(departmentId, fromDate, toDate);
        return Ok(stats);
    }
}

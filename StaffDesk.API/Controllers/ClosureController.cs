using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/tasks")]
[Authorize]
public class ClosureController : ApiControllerBase
{
    private readonly IClosureService _closureService;
    private readonly ITaskService _taskService;

    public ClosureController(
        IClosureService closureService,
        ITaskService taskService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _closureService = closureService;
        _taskService = taskService;
    }

    // ============================================
    // POST /v1/tasks/{taskId}/outcome - Set task outcome (WC-15)
    // ============================================
    [HttpPost("{taskId}/outcome")]
    public async Task<IActionResult> SetOutcome(int taskId, [FromBody] TaskOutcomeSetDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var outcome = await _closureService.SetOutcomeAsync(
                taskId,
                dto.Outcome,
                dto.Note,
                dto.ExternalReference,
                dto.ExternalReferenceLabel,
                employeeId.Value
            );

            return Ok(new
            {
                id = outcome.Id,
                taskId = outcome.TaskId,
                outcome = outcome.Outcome,
                note = outcome.Note,
                externalReference = outcome.ExternalReference,
                externalReferenceLabel = outcome.ExternalReferenceLabel,
                occurredAt = outcome.OccurredAt
            });
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
    // GET /v1/tasks/{taskId}/outcome - Get task outcome
    // ============================================
    [HttpGet("{taskId}/outcome")]
    public async Task<IActionResult> GetOutcome(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        var outcome = await _closureService.GetOutcomeByTaskIdAsync(taskId);
        if (outcome == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Outcome not found"));

        return Ok(new
        {
            id = outcome.Id,
            taskId = outcome.TaskId,
            outcome = outcome.Outcome,
            note = outcome.Note,
            externalReference = outcome.ExternalReference,
            externalReferenceLabel = outcome.ExternalReferenceLabel,
            occurredAt = outcome.OccurredAt
        });
    }

    // ============================================
    // GET /v1/tasks/{taskId}/outcomes - Get all outcomes for a task
    // ============================================
    [HttpGet("{taskId}/outcomes")]
    public async Task<IActionResult> GetOutcomes(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        var outcomes = await _closureService.GetOutcomesByTaskIdAsync(taskId);
        return Ok(outcomes.Select(o => new
        {
            id = o.Id,
            taskId = o.TaskId,
            outcome = o.Outcome,
            note = o.Note,
            externalReference = o.ExternalReference,
            externalReferenceLabel = o.ExternalReferenceLabel,
            occurredAt = o.OccurredAt
        }));
    }

    // ============================================
    // POST /v1/tasks/{taskId}/closure - Process closure (WC-17)
    // ============================================
    [HttpPost("{taskId}/closure")]
    public async Task<IActionResult> ProcessClosure(int taskId, [FromBody] TaskClosureDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var closure = await _closureService.ProcessClosureAsync(taskId, employeeId.Value, dto.ClosureNote);

            return Ok(new
            {
                taskId = closure.TaskId,
                isSlaBreached = closure.IsSlaBreached,
                wasReopened = closure.WasReopened,
                hasOverriddenAcceptance = closure.HasOverriddenAcceptance,
                closureNote = closure.ClosureNote,
                createdAt = closure.CreatedAt
            });
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
    // GET /v1/tasks/{taskId}/closure - Get task closure
    // ============================================
    [HttpGet("{taskId}/closure")]
    public async Task<IActionResult> GetClosure(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        var closure = await _closureService.GetClosureByTaskIdAsync(taskId);
        if (closure == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Closure not found"));

        return Ok(new
        {
            id = closure.Id,
            taskId = closure.TaskId,
            isSlaBreached = closure.IsSlaBreached,
            wasReopened = closure.WasReopened,
            hasOverriddenAcceptance = closure.HasOverriddenAcceptance,
            closureNote = closure.ClosureNote,
            createdAt = closure.CreatedAt
        });
    }

    // ============================================
    // GET /v1/tasks/{taskId}/has-closure - Check if task has closure
    // ============================================
    [HttpGet("{taskId}/has-closure")]
    public async Task<IActionResult> HasClosure(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        var hasClosure = await _closureService.HasClosureAsync(taskId);
        return Ok(new { taskId, hasClosure });
    }
}
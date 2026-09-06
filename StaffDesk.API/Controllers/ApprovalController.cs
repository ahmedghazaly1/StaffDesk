using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Entities;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/approvals")]
[Authorize]
public class ApprovalController : ApiControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly ITaskService _taskService;

    public ApprovalController(
        IApprovalService approvalService,
        ITaskService taskService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _approvalService = approvalService;
        _taskService = taskService;
    }

    // ============================================
    // POST /v1/approvals/task/{taskId} - Create approval for a task
    // ============================================
    [HttpPost("task/{taskId}")]
    [AdminOnly]
    public async Task<IActionResult> CreateApproval(int taskId, [FromBody] ApprovalCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var steps = dto.Steps.Select(s => (s.ApproverId, s.Role, s.Order)).ToList();
            
            var approval = await _approvalService.CreateApprovalAsync(
                taskId,
                dto.Name,
                dto.Description,
                dto.IsRequired,
                steps,
                employeeId.Value
            );

            return CreatedAtAction(nameof(GetApproval), new { id = approval.Id }, MapToResponse(approval));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // ============================================
    // GET /v1/approvals/{id} - Get approval by ID
    // ============================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetApproval(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var approval = await _approvalService.GetApprovalByIdAsync(id);
        if (approval == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Approval not found"));

        // Check if user can view this approval
        var task = await _taskService.GetTaskByIdAsync(approval.TaskId, employeeId.Value);
        if (task == null)
            return Forbid();

        return Ok(MapToResponse(approval));
    }

    // ============================================
    // GET /v1/approvals/task/{taskId} - Get approval by task ID
    // ============================================
    [HttpGet("task/{taskId}")]
    public async Task<IActionResult> GetApprovalByTask(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        // Check if user can view this task
        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return Forbid();

        var approval = await _approvalService.GetApprovalByTaskIdAsync(taskId);
        if (approval == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "No approval found for this task"));

        return Ok(MapToResponse(approval));
    }

    // ============================================
    // PATCH /v1/approvals/steps/{stepId} - Review an approval step
    // ============================================
    [HttpPatch("steps/{stepId}")]
    public async Task<IActionResult> ReviewStep(int stepId, [FromBody] ApprovalStepUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var step = await _approvalService.ReviewStepAsync(
                stepId,
                employeeId.Value,
                dto.State,
                dto.DecisionNote
            );

            return Ok(new
            {
                id = step.Id,
                state = step.State,
                decisionNote = step.DecisionNote,
                decisionAt = step.DecisionAt
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
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // ============================================
    // POST /v1/approvals/steps/{stepId}/skip - Skip an approval step (Admin/Manager only)
    // ============================================
    [HttpPost("steps/{stepId}/skip")]
    [AdminOnly]
    public async Task<IActionResult> SkipStep(int stepId, [FromBody] SkipStepDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var step = await _approvalService.SkipStepAsync(
                stepId,
                employeeId.Value,
                dto.Reason
            );

            return Ok(new
            {
                id = step.Id,
                state = step.State,
                decisionNote = step.DecisionNote,
                decisionAt = step.DecisionAt
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
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // POST /v1/approvals/steps/{stepId}/reassign - AP-6: Reassign pending step
    [HttpPost("steps/{stepId}/reassign")]
    [AdminOnly]
    public async Task<IActionResult> ReassignStep(int stepId, [FromBody] ApprovalStepReassignDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var step = await _approvalService.ReassignStepAsync(
                stepId, dto.NewApproverId, employeeId.Value, dto.Reason);

            return Ok(new
            {
                id = step.Id,
                approverId = step.ApproverId,
                state = step.State
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
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // ============================================
    // GET /v1/approvals/pending - Get pending approvals for current user
    // ============================================
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var pendingSteps = await _approvalService.GetPendingApprovalsForUserAsync(employeeId.Value);
        
        var result = pendingSteps.Select(s => new
        {
            s.Id,
            s.TaskId,
            taskKey = s.Task?.Key,
            taskTitle = s.Task?.Title,
            s.Order,
            s.State,
            s.CreatedAt,
            s.Role
        });

        return Ok(result);
    }

    // ============================================
    // GET /v1/approvals/task/{taskId}/status - Check if task is fully approved
    // ============================================
    [HttpGet("task/{taskId}/status")]
    public async Task<IActionResult> GetApprovalStatus(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        // Check if user can view this task
        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return Forbid();

        var isFullyApproved = await _approvalService.IsFullyApprovedAsync(taskId);
        var approval = await _approvalService.GetApprovalByTaskIdAsync(taskId);

        return Ok(new
        {
            taskId = taskId,
            isFullyApproved = isFullyApproved,
            hasApproval = approval != null,
            isApprovalRequired = task.IsApprovalRequired,
            totalSteps = approval?.Steps?.Count ?? 0,
            approvedSteps = approval?.Steps?.Count(s => s.State == "APPROVED") ?? 0,
            rejectedSteps = approval?.Steps?.Count(s => s.State == "REJECTED") ?? 0,
            pendingSteps = approval?.Steps?.Count(s => s.State == "PENDING") ?? 0,
            skippedSteps = approval?.Steps?.Count(s => s.State == "SKIPPED") ?? 0
        });
    }

    // DELETE /v1/approvals/{id} - Delete an approval (Admin/Manager only)
    // ============================================
    [HttpDelete("{id}")]
    [AdminOnly]
    public async Task<IActionResult> DeleteApproval(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _approvalService.DeleteApprovalAsync(id, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Approval not found"));

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // ============================================
    // Helper: Map TaskApproval to ApprovalResponseDto
    // ============================================
    private ApprovalResponseDto MapToResponse(TaskApproval approval)
    {
        var steps = approval.Steps?.OrderBy(s => s.Order).Select(s => new ApprovalStepResponseDto
        {
            Id = s.Id,
            ApproverId = s.ApproverId,
            ApproverName = s.Approver?.FullName,
            ApproverLevel = s.Approver?.Level?.Name,
            Role = s.Role,
            Order = s.Order,
            State = s.State,
            DecisionNote = s.DecisionNote,
            DecisionAt = s.DecisionAt,
            CreatedAt = s.CreatedAt
        }).ToList() ?? new List<ApprovalStepResponseDto>();

        return new ApprovalResponseDto
        {
            Id = approval.Id,
            TaskId = approval.TaskId,
            Name = approval.Name,
            Description = approval.Description,
            IsRequired = approval.IsRequired,
            Steps = steps,
            CreatedAt = approval.CreatedAt,
            IsFullyApproved = steps.All(s => s.State == "APPROVED" || s.State == "SKIPPED"),
            IsRejected = steps.Any(s => s.State == "REJECTED")
        };
    }
}

// ============================================
// Request DTOs
// ============================================
public class SkipStepDto
{
    public string? Reason { get; set; }
}
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
[Route("v1/sla")]
[Authorize]
public class SlaController : ApiControllerBase
{
    private readonly ISlaService _slaService;
    private readonly ISlaRepository _slaRepository;
    private readonly ITaskService _taskService;

    public SlaController(
        ISlaService slaService,
        ISlaRepository slaRepository,
        ITaskService taskService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _slaService = slaService;
        _slaRepository = slaRepository;
        _taskService = taskService;
    }

    // ============================================
    // GET /v1/sla/policies - List all SLA policies
    // ============================================
    [HttpGet("policies")]
    [AdminOnly]
    public async Task<IActionResult> GetPolicies()
    {
        var policies = await _slaRepository.GetAllPoliciesAsync();
        return Ok(policies.Select(MapPolicyToResponse));
    }

    // ============================================
    // GET /v1/sla/policies/department/{departmentId}
    // ============================================
    [HttpGet("policies/department/{departmentId}")]
    [AdminOnly]
    public async Task<IActionResult> GetPoliciesForDepartment(int departmentId)
    {
        var policies = await _slaRepository.GetPoliciesForDepartmentAsync(departmentId);
        return Ok(policies.Select(MapPolicyToResponse));
    }

    // ============================================
    // GET /v1/sla/policies/{id}
    // ============================================
    [HttpGet("policies/{id}")]
    [AdminOnly]
    public async Task<IActionResult> GetPolicy(int id)
    {
        var policy = await _slaRepository.GetPolicyByIdAsync(id);
        if (policy == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "SLA policy not found"));

        return Ok(MapPolicyToResponse(policy));
    }

    // ============================================
    // POST /v1/sla/policies - Create SLA policy
    // ============================================
    [HttpPost("policies")]
    [AdminOnly]
    public async Task<IActionResult> CreatePolicy([FromBody] SlaPolicyCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            // A (DepartmentId, Priority) pair is unique at the DB level; check first and return a
            // proper 409 instead of letting a duplicate insert surface as an unhandled 500 from
            // the DbUpdateException the unique index throws.
            var existing = await _slaRepository.GetPolicyAsync(dto.DepartmentId, dto.Priority);
            if (existing != null)
                return Conflict(ApiError.Build(TaskErrorCodes.DuplicateResource,
                    $"An SLA policy already exists for department {dto.DepartmentId} and priority {dto.Priority}"));

            var policy = new SlaPolicy
            {
                DepartmentId = dto.DepartmentId,
                Priority = dto.Priority,
                ResponseTargetMinutes = dto.ResponseTargetMinutes,
                ResolutionTargetMinutes = dto.ResolutionTargetMinutes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _slaRepository.SavePolicyAsync(policy);

            // Audit the creation
            await _slaService.LogSlaEventAsync(
                employeeId.Value,
                "SLA_POLICY_CREATED",
                created.Id.ToString(),
                "SUCCESS",
                new { policy.DepartmentId, policy.Priority, policy.ResponseTargetMinutes, policy.ResolutionTargetMinutes }
            );

            return CreatedAtAction(nameof(GetPolicy), new { id = created.Id }, MapPolicyToResponse(created));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // PUT /v1/sla/policies/{id} - Update SLA policy
    // ============================================
    [HttpPut("policies/{id}")]
    [AdminOnly]
    public async Task<IActionResult> UpdatePolicy(int id, [FromBody] SlaPolicyUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var policy = await _slaRepository.GetPolicyByIdAsync(id);
            if (policy == null)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "SLA policy not found"));

            var oldValues = new { policy.ResponseTargetMinutes, policy.ResolutionTargetMinutes };

            policy.ResponseTargetMinutes = dto.ResponseTargetMinutes;
            policy.ResolutionTargetMinutes = dto.ResolutionTargetMinutes;
            policy.UpdatedAt = DateTime.UtcNow;

            var updated = await _slaRepository.SavePolicyAsync(policy);

            // Audit the update
            await _slaService.LogSlaEventAsync(
                employeeId.Value,
                "SLA_POLICY_UPDATED",
                updated.Id.ToString(),
                "SUCCESS",
                new { Old = oldValues, New = new { updated.ResponseTargetMinutes, updated.ResolutionTargetMinutes } }
            );

            return Ok(MapPolicyToResponse(updated));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // DELETE /v1/sla/policies/{id} - Delete SLA policy
    // ============================================
    [HttpDelete("policies/{id}")]
    [AdminOnly]
    public async Task<IActionResult> DeletePolicy(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var policy = await _slaRepository.GetPolicyByIdAsync(id);
        if (policy == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "SLA policy not found"));

        var deleted = await _slaRepository.DeletePolicyAsync(id);
        if (!deleted)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "SLA policy not found"));

        // Audit the deletion
        await _slaService.LogSlaEventAsync(
            employeeId.Value,
            "SLA_POLICY_DELETED",
            id.ToString(),
            "SUCCESS",
            new { policy.DepartmentId, policy.Priority }
        );

        return NoContent();
    }

    // ============================================
// GET /v1/sla/task/{taskId} - Get SLA state for a task
// ============================================
[HttpGet("task/{taskId}")]
public async Task<IActionResult> GetTaskSlaState(int taskId)
{
    var employeeId = await GetCurrentEmployeeIdAsync();
    if (employeeId == null)
        return NoEmployeeLinkError();

    try
    {
        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        var slaState = await _slaService.GetTaskSlaStateAsync(task);

        // Map escalations to DTOs
        var escalationDtos = slaState.Escalations.Select(e => new SlaEscalationResponseDto
        {
            Id = e.Id,
            Level = e.Level,
            NotifiedAt = e.NotifiedAt,
            Target = _slaService.GetEscalationLevels().FirstOrDefault(l => l.Level == e.Level).Target ?? "Unknown"
        }).ToList();

        return Ok(new SlaTaskStateResponseDto
        {
            TaskId = task.Id,
            TaskKey = task.Key,
            Priority = task.Priority,
            ResponseTargetAt = slaState.ResponseTargetAt,
            ResolutionTargetAt = slaState.ResolutionTargetAt,
            BreachState = slaState.BreachState,
            RemainingMinutes = slaState.RemainingMinutes,
            IsBreached = slaState.IsBreached,
            IsAtRisk = slaState.IsAtRisk,
            Escalations = escalationDtos
        });
    }
    catch (TaskDomainException ex)
    {
        return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
    }
}

    // ============================================
    // POST /v1/sla/evaluate - Force SLA evaluation (Admin only)
    // ============================================
    [HttpPost("evaluate")]
    [AdminOnly]
    public async Task<IActionResult> EvaluateAllTasks()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var results = await _slaService.EvaluateAllTasksAsync();

            return Ok(new
            {
                evaluated = results.Count,
                results = results.Select(r => new
                {
                    r.TaskId,
                    r.TaskKey,
                    r.PreviousState,
                    r.NewState,
                    r.EscalationsSent,
                    r.Reason
                })
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/sla/escalations/task/{taskId}
    // ============================================
    [HttpGet("escalations/task/{taskId}")]
    public async Task<IActionResult> GetTaskEscalations(int taskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByIdAsync(taskId, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        var escalations = await _slaRepository.GetEscalationsForTaskAsync(taskId);
        return Ok(escalations.Select(e => new SlaEscalationResponseDto
        {
            Id = e.Id,
            Level = e.Level,
            NotifiedAt = e.NotifiedAt,
            Target = _slaService.GetEscalationLevels().FirstOrDefault(l => l.Level == e.Level).Target ?? "Unknown"
        }));
    }

    // ============================================
    // GET /v1/sla/escalation-levels
    // ============================================
    [HttpGet("escalation-levels")]
    public IActionResult GetEscalationLevels()
    {
        var levels = _slaService.GetEscalationLevels();
        return Ok(levels.Select(l => new
        {
            l.Level,
            l.DelayMinutes,
            l.Target
        }));
    }

    // ============================================
    // Helper: Map SlaPolicy to DTO
    // ============================================
    private SlaPolicyResponseDto MapPolicyToResponse(SlaPolicy policy)
    {
        return new SlaPolicyResponseDto
        {
            Id = policy.Id,
            DepartmentId = policy.DepartmentId,
            DepartmentName = policy.Department?.Name ?? string.Empty,
            Priority = policy.Priority,
            ResponseTargetMinutes = policy.ResponseTargetMinutes,
            ResponseTargetHours = (decimal)policy.ResponseTargetMinutes / 60,
            ResolutionTargetMinutes = policy.ResolutionTargetMinutes,
            ResolutionTargetHours = (decimal)policy.ResolutionTargetMinutes / 60,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };
    }
}
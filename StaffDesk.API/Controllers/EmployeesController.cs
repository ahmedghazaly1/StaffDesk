using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ITaskService _taskService;
    private readonly IUserRepository _userRepository;
    private readonly IAuthSessionService _sessions;

    public EmployeesController(
        IEmployeeService employeeService,
        ITaskService taskService,
        IUserRepository userRepository,
        IAuthSessionService sessions)
    {
        _employeeService = employeeService;
        _taskService = taskService;
        _userRepository = userRepository;
        _sessions = sessions;
    }

    private int GetActorUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : 0;
    }

    // TaskService methods take Employee.Id (see the NOTE in TaskService), not User.Id - resolve it
    // the same way TasksController does, via the JWT's User -> User.EmployeeId link.
    private async Task<int?> GetActorEmployeeIdAsync()
    {
        var user = await _userRepository.GetByIdAsync(GetActorUserId());
        return user?.EmployeeId;
    }

    private static EmployeeResponseDto ToDto(StaffDesk.Core.Entities.Employee e) => new EmployeeResponseDto
    {
        Id = e.Id,
        FullName = e.FullName,
        JobTitle = e.JobTitle,
        DepartmentName = e.Department?.Name ?? string.Empty,
        DepartmentId = e.DepartmentId,
        LevelId = e.LevelId,
        LevelName = e.Level?.Name ?? string.Empty,
        LevelRank = e.Level?.Rank ?? 0,
        ManagerId = e.ManagerId,
        ManagerName = e.Manager?.FullName,
        IsActive = e.IsActive
    };

    // GET /v1/employees - List employees with search, pagination, and filters (OR-14)
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? limit,
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] int? levelId,
        [FromQuery] int? managerId,
        [FromQuery] bool? isActive)
    {
        int pageValue = page.GetValueOrDefault(1);
        int limitValue = limit.GetValueOrDefault(10);

        if (pageValue < 1) pageValue = 1;
        if (limitValue < 1) limitValue = 10;
        if (limitValue > 100) limitValue = 100;

        var employees = await _employeeService.GetAllAsync(pageValue, limitValue, search, departmentId, levelId, managerId, isActive);
        var total = await _employeeService.GetTotalCountAsync(search, departmentId, levelId, managerId, isActive);

        var response = new PaginatedResponseDto<EmployeeResponseDto>
        {
            Data = employees.Select(ToDto),
            Page = pageValue,
            Limit = limitValue,
            Total = total
        };

        return Ok(response);
    }

    // GET /v1/employees/{id} - Get single employee
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        if (employee == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Employee not found"));

        StaffDesk.API.Common.ConcurrencyHelper.SetETag(Response, employee.UpdatedAt);
        return Ok(ToDto(employee));
    }

    // POST /v1/employees - Create a new employee
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmployeeCreateDto dto)
    {
        try
        {
            var employee = await _employeeService.CreateAsync(
                dto.FullName,
                dto.JobTitle,
                dto.DepartmentId,
                dto.LevelId
            );

            var created = await _employeeService.GetByIdAsync(employee.Id) ?? employee;
            return CreatedAtAction(nameof(GetById), new { id = employee.Id }, ToDto(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // PUT /v1/employees/{id} - Update an employee (Admin only)
    [HttpPut("{id}")]
    [AdminOnly]
    public async Task<IActionResult> Update(int id, [FromBody] EmployeeUpdateDto dto)
    {
        var current = await _employeeService.GetByIdAsync(id);
        if (current == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Employee not found"));
        if (StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(Request, current.UpdatedAt) is { } precondition)
            return StatusCode(precondition.Status, precondition.Body);

        try
        {
            var employee = await _employeeService.UpdateAsync(
                id,
                dto.FullName,
                dto.JobTitle,
                dto.DepartmentId,
                dto.LevelId,
                dto.ManagerId,
                dto.IsActive,
                GetActorUserId()
            );

            if (current.IsActive && !employee.IsActive)
                await _sessions.RevokeAllForEmployeeAsync(id, "deactivation");

            StaffDesk.API.Common.ConcurrencyHelper.SetETag(Response, employee.UpdatedAt);
            return Ok(ToDto(employee));
        }
        catch (ReportingCycleException ex)
        {
            return UnprocessableEntity(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // DELETE /v1/employees/{id} - Deactivate an employee (Admin only).
    // Employees are never hard-deleted: they stay readable and attached to
    // historical records (OR-8), just no longer assignable or able to log in.
    [HttpDelete("{id}")]
    [AdminOnly]
    public async Task<IActionResult> Deactivate(int id, [FromQuery] int? reassignTo = null)
    {
        // TR-28: count the employee's active tasks up front (still valid whether or not we reassign),
        // then - if a target was given - reassign FIRST so there's no moment where an inactive
        // employee still holds active tasks, and only then deactivate.
        var activeTaskCount = await _taskService.CountActiveTasksForEmployeeAsync(id);

        if (reassignTo.HasValue)
        {
            var actorEmployeeId = await GetActorEmployeeIdAsync();
            if (actorEmployeeId == null)
            {
                return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden, "Your account is not linked to an employee record, so you cannot reassign tasks."));
            }

            try
            {
                await _taskService.ReassignAllActiveTasksAsync(id, reassignTo.Value, actorEmployeeId.Value);
            }
            catch (StaffDesk.Core.Exceptions.TaskDomainException ex)
            {
                return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
            }
        }

        var deactivated = await _employeeService.DeactivateAsync(id, GetActorUserId());
        if (!deactivated)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Employee not found"));

        await _sessions.RevokeAllForEmployeeAsync(id, "deactivation");

        return Ok(new DeactivateResponseDto
        {
            Deactivated = true,
            ActiveTaskCount = activeTaskCount,
            ReassignedTo = reassignTo
        });
    }

    // POST /v1/employees/{fromId}/handover - WC-30: move all active tasks between employees
    [HttpPost("{fromId}/handover")]
    public async Task<IActionResult> Handover(int fromId, [FromBody] HandoverRequestDto dto)
    {
        var actorEmployeeId = await GetActorEmployeeIdAsync();
        if (actorEmployeeId == null)
            return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden, "Your account is not linked to an employee record."));

        try
        {
            var count = await _taskService.CountActiveTasksForEmployeeAsync(fromId);
            await _taskService.ReassignAllActiveTasksAsync(fromId, dto.ToEmployeeId, actorEmployeeId.Value);
            return Ok(new { fromEmployeeId = fromId, toEmployeeId = dto.ToEmployeeId, tasksReassigned = count });
        }
        catch (StaffDesk.Core.Exceptions.TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // GET /v1/employees/{id}/reports - Direct reports of one employee
    [HttpGet("{id}/reports")]
    public async Task<IActionResult> GetDirectReports(int id)
    {
        try
        {
            var reports = await _employeeService.GetDirectReportsAsync(id);
            return Ok(reports.Select(ToDto));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
        }
    }

    // GET /v1/employees/{id}/chain - Reporting chain upward to the top
    [HttpGet("{id}/chain")]
    public async Task<IActionResult> GetReportingChain(int id)
    {
        try
        {
            var chain = await _employeeService.GetReportingChainAsync(id);
            return Ok(chain.Select(ToDto));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
        }
    }

    // GET /v1/employees/{id}/activity - Level/manager/department/active-flag change history (OR-13)
    [HttpGet("{id}/activity")]
    public async Task<IActionResult> GetActivity(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        if (employee == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Employee not found"));

        var activity = await _employeeService.GetActivityAsync(id);
        return Ok(activity.Select(a => new
        {
            a.Id,
            a.ActorUserId,
            a.Field,
            a.OldValue,
            a.NewValue,
            a.CreatedAt
        }));
    }

    // PATCH /v1/employees/{id}/manager - Update just the manager
    [HttpPatch("{id}/manager")]
    [AdminOnly]
    public async Task<IActionResult> UpdateManager(int id, [FromBody] EmployeeUpdateManagerDto dto)
    {
        try
        {
            var employee = await _employeeService.UpdateManagerAsync(id, dto.ManagerId, GetActorUserId());
            return Ok(new
            {
                id = employee.Id,
                fullName = employee.FullName,
                managerId = employee.ManagerId
            });
        }
        catch (ReportingCycleException ex)
        {
            return UnprocessableEntity(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // DG-7: queue subject-access export
    [HttpPost("{id}/data-export")]
    [Authorize(Roles = "Admin,HR_ADMIN")]
    public async Task<IActionResult> QueueDataExport(int id, [FromServices] IGovernanceService governance)
    {
        var actorEmployeeId = await GetActorEmployeeIdAsync();
        if (actorEmployeeId == null)
            return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden, "Your account is not linked to an employee record."));

        try
        {
            var export = await governance.QueueSubjectAccessExportAsync(id, actorEmployeeId.Value);
            return Accepted(new { export.Id, export.State, export.JobId, downloadPath = $"/v1/audit/exports/{export.Id}/download" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Employee not found"));
        }
    }

    // DG-8/DG-9: queue erasure (pseudonymisation)
    [HttpPost("{id}/erasure")]
    [Authorize(Roles = "HR_ADMIN")]
    public async Task<IActionResult> QueueErasure(int id, [FromServices] IGovernanceService governance)
    {
        var actorEmployeeId = await GetActorEmployeeIdAsync();
        if (actorEmployeeId == null)
            return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden, "Your account is not linked to an employee record."));

        try
        {
            var (jobId, message) = await governance.QueueErasureAsync(id, actorEmployeeId.Value);
            return Accepted(new { jobId, message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Employee not found"));
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(ApiError.Build(TaskErrorCodes.LegalHoldActive, ex.Message));
        }
    }
}

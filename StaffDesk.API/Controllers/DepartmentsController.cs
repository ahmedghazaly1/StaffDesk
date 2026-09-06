using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;


namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/departments")]
[Authorize]
public class DepartmentsController : ApiControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly IEmployeeService _employeeService;
    private readonly ITaskService _taskService;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;

    public DepartmentsController(
        IDepartmentService departmentService,
        IEmployeeService employeeService,
        ITaskService taskService,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository)
        : base(userRepository)
    {
        _departmentService = departmentService;
        _employeeService = employeeService;
        _taskService = taskService;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
    }

    // Same visibility rule as the task list: ADMIN sees any department, everyone else only their own.
    private async Task<bool> CanViewDepartmentReportingAsync(int departmentId)
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId)) return false;

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return false;
        if (user.Role == "Admin") return true;

        var employee = await _taskService.GetUserEmployeeAsync(user.EmployeeId ?? 0);
        return employee != null && employee.DepartmentId == departmentId;
    }

    // GET /v1/departments - List all departments
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var departments = await _departmentService.GetAllAsync();
        var response = departments.Select(d => new DepartmentListResponseDto
        {
            Id = d.Id,
            Name = d.Name,
            Location = d.Location,
            ManagerId = d.ManagerId,
            ManagerName = d.Manager?.FullName
        });
        return Ok(response);
    }

    // GET /v1/departments/{id} - Get single department with employee count
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Department not found"));

        var response = new DepartmentDetailResponseDto
        {
            Id = department.Id,
            Name = department.Name,
            Location = department.Location,
            EmployeeCount = department.Employees?.Count ?? 0,
            ManagerId = department.ManagerId,
            ManagerName = department.Manager?.FullName
        };
        return Ok(response);
    }

    // GET /v1/departments/{id}/employees - Get employees for a department
    [HttpGet("{id}/employees")]
    public async Task<IActionResult> GetEmployees(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Department not found"));

        var employees = await _employeeService.GetByDepartmentIdAsync(id);
        var response = employees.Select(e => new EmployeeResponseDto
        {
            Id = e.Id,
            FullName = e.FullName,
            JobTitle = e.JobTitle,
            DepartmentName = e.Department?.Name ?? string.Empty
        });
        return Ok(response);
    }

    // POST /v1/departments - Create a new department (Admin only, consistent with update/delete
    // and with POST /v1/seniority-levels).
    [HttpPost]
    [AdminOnly]
    public async Task<IActionResult> Create([FromBody] DepartmentCreateDto dto)
    {
        try
        {
            var department = await _departmentService.CreateAsync(dto.Name, dto.Location);
            var response = new DepartmentDetailResponseDto
            {
                Id = department.Id,
                Name = department.Name,
                Location = department.Location,
                EmployeeCount = 0
            };
            return CreatedAtAction(nameof(GetById), new { id = department.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // PUT /v1/departments/{id} - Update a department (Admin only)
[HttpPut("{id}")]
[AdminOnly]  // Only admins can update
public async Task<IActionResult> Update(int id, [FromBody] DepartmentCreateDto dto)
{
    try
    {
        var department = await _departmentService.UpdateAsync(id, dto.Name, dto.Location);
        var response = new DepartmentDetailResponseDto
        {
            Id = department.Id,
            Name = department.Name,
            Location = department.Location,
            EmployeeCount = department.Employees?.Count ?? 0,
            ManagerId = department.ManagerId,
            ManagerName = department.Manager?.FullName
        };
        return Ok(response);
    }
    catch (ArgumentException ex)
    {
        if (ex.Message.Contains("not found"))
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
        return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
    }
}

// DELETE /v1/departments/{id} - Delete a department (Admin only)
[HttpDelete("{id}")]
[AdminOnly]  // Only admins can delete
public async Task<IActionResult> Delete(int id)
{
    try
    {
        var deleted = await _departmentService.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Department not found"));

        return NoContent(); // 204 No Content
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        // Blocked by a RESTRICT foreign key (employees, tasks, SLA policies, etc.) - a clean 409
        // naming the reason, instead of the DB's own FK violation surfacing as an unhandled 500.
        return Conflict(ApiError.Build(TaskErrorCodes.DuplicateResource, ex.Message));
    }
}

// ============================================
    // NEW: Update department manager
    // ============================================
    [HttpPatch("{id}/manager")]
    [AdminOnly]
    public async Task<IActionResult> UpdateManager(int id, [FromBody] DepartmentUpdateManagerDto dto)
    {
        try
        {
            var department = await _departmentService.UpdateManagerAsync(id, dto.ManagerId);
            return Ok(new
            {
                id = department.Id,
                name = department.Name,
                managerId = department.ManagerId,
                managerName = department.Manager?.FullName
            });
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, ex.Message));
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // GET /v1/departments/{id}/task-summary (RP-1)
    [HttpGet("{id}/task-summary")]
    public async Task<IActionResult> GetTaskSummary(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Department not found"));

        if (!await CanViewDepartmentReportingAsync(id))
            return Forbid();

        var (statusCounts, overdue, unassigned) = await _taskService.GetDepartmentTaskSummaryAsync(id);

        return Ok(new DepartmentTaskSummaryDto
        {
            DepartmentId = id,
            StatusCounts = statusCounts,
            Overdue = overdue,
            Unassigned = unassigned
        });
    }

    // GET /v1/departments/{id}/workload (RP-2)
    [HttpGet("{id}/workload")]
    public async Task<IActionResult> GetWorkload(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Department not found"));

        if (!await CanViewDepartmentReportingAsync(id))
            return Forbid();

        var workload = await _taskService.GetDepartmentWorkloadAsync(id);

        var data = workload.Select(w => new DepartmentWorkloadEntryDto
        {
            EmployeeId = w.EmployeeId,
            EmployeeName = w.EmployeeName,
            PriorityCounts = w.PriorityCounts,
            TotalActive = w.PriorityCounts.Values.Sum()
        });

        return Ok(data);
    }

    // ============================================
    // Default Definition of Done (WC-12) - Admin/dept-manager only to manage; open read.
    // ============================================
    [HttpGet("{id}/default-criteria")]
    public async Task<IActionResult> GetDefaultCriteria(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Department not found"));

        var criteria = await _taskService.GetDepartmentDefaultCriteriaAsync(id);
        return Ok(criteria.Select(c => new DepartmentDefaultCriterionResponseDto
        {
            Id = c.Id,
            DepartmentId = c.DepartmentId,
            Text = c.Text,
            Position = c.Position
        }));
    }

    [HttpPost("{id}/default-criteria")]
    public async Task<IActionResult> AddDefaultCriterion(int id, [FromBody] DepartmentDefaultCriterionCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var criterion = await _taskService.AddDepartmentDefaultCriterionAsync(id, employeeId.Value, dto.Text);
            return StatusCode(201, new DepartmentDefaultCriterionResponseDto
            {
                Id = criterion.Id,
                DepartmentId = criterion.DepartmentId,
                Text = criterion.Text,
                Position = criterion.Position
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpDelete("{id}/default-criteria/{criterionId}")]
    public async Task<IActionResult> DeleteDefaultCriterion(int id, int criterionId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _taskService.DeleteDepartmentDefaultCriterionAsync(criterionId, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Default criterion not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("{id}/triagers")]
    public async Task<IActionResult> GetTriagers(int id)
    {
        var triagers = await _departmentRepository.GetTriagersAsync(id);
        return Ok(triagers.Select(t => new
        {
            t.Id,
            t.EmployeeId,
            EmployeeName = t.Employee?.FullName,
            t.CreatedAt
        }));
    }

    [HttpPost("{id}/triagers")]
    [AdminOnly]
    public async Task<IActionResult> AddTriager(int id, [FromBody] AddTriagerDto dto)
    {
        try
        {
            var triager = await _departmentRepository.AddTriagerAsync(id, dto.EmployeeId);
            return Ok(new { triager.Id, triager.EmployeeId, triager.DepartmentId });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiError.Build(TaskErrorCodes.DuplicateResource, ex.Message));
        }
    }

    [HttpDelete("{id}/triagers/{employeeId}")]
    [AdminOnly]
    public async Task<IActionResult> RemoveTriager(int id, int employeeId)
    {
        var removed = await _departmentRepository.RemoveTriagerAsync(id, employeeId);
        if (!removed) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Triager not found"));
        return NoContent();
    }
}
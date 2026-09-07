using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

// Part B Module 2 - Intake and Triage (WC-1..WC-9).
[ApiController]
[Route("v1/task-requests")]
[Authorize]
public class TaskRequestsController : ApiControllerBase
{
    private readonly ITaskRequestService _requestService;

    public TaskRequestsController(ITaskRequestService requestService, IUserRepository userRepository)
        : base(userRepository)
    {
        _requestService = requestService;
    }

    // ============================================
    // POST /v1/task-requests - WC-2: any authenticated employee, any department.
    // ============================================
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] TaskRequestCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var request = await _requestService.SubmitAsync(
                employeeId.Value, dto.Title, dto.Description, dto.DepartmentId,
                dto.BusinessJustification, dto.DesiredByDate);

            return CreatedAtAction(nameof(GetById), new { id = request.Id }, MapToResponse(request));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/task-requests - requests the caller submitted ("My Requests"). Department intake
    // for triagers lives on GET /v1/task-requests/triage-queue.
    // ============================================
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? departmentId = null,
        [FromQuery] string? status = null,
        [FromQuery] int? page = 1,
        [FromQuery] int? limit = 25)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var (items, total) = await _requestService.GetListAsync(employeeId.Value, departmentId, status, page, limit);

            var response = new TaskRequestListResponseDto
            {
                Data = items.Select(MapToResponse).ToList(),
                Page = page ?? 1,
                Limit = limit ?? 25,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / (limit ?? 25))
            };

            return Ok(response);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/task-requests/triage-queue?departmentId=X (WC-8) - literal segment, must be
    // registered ahead of GET /{id} so routing doesn't try to parse "triage-queue" as an id
    // (same precedent as GET /v1/tasks/mine in TasksController).
    // ============================================
    [HttpGet("triage-queue")]
    public async Task<IActionResult> GetTriageQueue([FromQuery] int? departmentId = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var queue = await _requestService.GetTriageQueueAsync(departmentId, employeeId.Value);
            var now = DateTime.UtcNow;

            var data = queue.Select(r =>
            {
                var dto = MapToResponse(r);
                return new TaskRequestQueueItemDto
                {
                    Id = dto.Id,
                    Title = dto.Title,
                    Description = dto.Description,
                    RequestedById = dto.RequestedById,
                    RequestedByName = dto.RequestedByName,
                    DepartmentId = dto.DepartmentId,
                    DepartmentName = dto.DepartmentName,
                    BusinessJustification = dto.BusinessJustification,
                    DesiredByDate = dto.DesiredByDate,
                    Status = dto.Status,
                    DeclineReasonCategory = dto.DeclineReasonCategory,
                    DeclineNote = dto.DeclineNote,
                    MergedIntoRequestId = dto.MergedIntoRequestId,
                    CreatedTaskId = dto.CreatedTaskId,
                    CreatedTaskKey = dto.CreatedTaskKey,
                    SubmittedAt = dto.SubmittedAt,
                    TriageDecidedAt = dto.TriageDecidedAt,
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt,
                    AgeHours = Math.Round((now - r.SubmittedAt).TotalHours, 1)
                };
            });

            return Ok(data);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/task-requests/{id}
    // ============================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var request = await _requestService.GetRequestAsync(id, employeeId.Value);
        if (request == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Request not found"));

        return Ok(MapToResponse(request));
    }

    // ============================================
    // POST /v1/task-requests/{id}/accept (WC-6/WC-7) - triager only.
    // ============================================
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var request = await _requestService.AcceptAsync(id, employeeId.Value);
            return Ok(MapToResponse(request));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // POST /v1/task-requests/{id}/decline (WC-4/WC-7) - triager only.
    // ============================================
    [HttpPost("{id}/decline")]
    public async Task<IActionResult> Decline(int id, [FromBody] TaskRequestDeclineDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var request = await _requestService.DeclineAsync(id, employeeId.Value, dto.ReasonCategory, dto.Note);
            return Ok(MapToResponse(request));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // POST /v1/task-requests/{id}/merge (WC-5/WC-7) - triager only.
    // ============================================
    [HttpPost("{id}/merge")]
    public async Task<IActionResult> Merge(int id, [FromBody] TaskRequestMergeDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var request = await _requestService.MergeAsync(id, employeeId.Value, dto.MergedIntoRequestId);
            return Ok(MapToResponse(request));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // GET /v1/task-requests/metrics/triage-latency - WC-9
    [HttpGet("metrics/triage-latency")]
    public async Task<IActionResult> GetTriageLatency(
        [FromQuery] int? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        if (departmentId.HasValue && !await _requestService.IsTriageAuthorityAsync(employeeId.Value, departmentId.Value))
            return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden, "Not authorized to view triage metrics for this department"));

        var metrics = await _requestService.GetTriageLatencyMetricsAsync(departmentId, fromDate, toDate);
        return Ok(metrics);
    }

    private static TaskRequestResponseDto MapToResponse(TaskRequest r)
    {
        return new TaskRequestResponseDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            RequestedById = r.RequestedById,
            RequestedByName = r.RequestedBy?.FullName ?? string.Empty,
            DepartmentId = r.DepartmentId,
            DepartmentName = r.Department?.Name ?? string.Empty,
            BusinessJustification = r.BusinessJustification,
            DesiredByDate = r.DesiredByDate,
            Status = r.Status,
            DeclineReasonCategory = r.DeclineReasonCategory,
            DeclineNote = r.DeclineNote,
            MergedIntoRequestId = r.MergedIntoRequestId,
            CreatedTaskId = r.CreatedTaskId,
            CreatedTaskKey = r.CreatedTask?.Key,
            SubmittedAt = r.SubmittedAt,
            TriageDecidedAt = r.TriageDecidedAt,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };
    }
}

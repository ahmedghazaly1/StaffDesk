using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Constants;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/tasks/bulk")]
[Authorize]
public class BulkController : ApiControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IJobService _jobService;

    public BulkController(
        ITaskService taskService,
        IJobService jobService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _taskService = taskService;
        _jobService = jobService;
    }

    [HttpPatch("status")]
    public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkStatusUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        if (dto.TaskIds == null || !dto.TaskIds.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No task IDs provided"));
        if (string.IsNullOrWhiteSpace(dto.Status))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "Status is required"));

        if (dto.TaskIds.Count > BulkOperationLimits.AsyncThreshold)
        {
            var job = await _jobService.EnqueueAsync(JobTypes.BulkStatus, new BulkJobPayload
            {
                TaskIds = dto.TaskIds,
                UserId = employeeId.Value,
                Status = dto.Status,
                Reason = dto.Reason
            });
            return Accepted(new BulkJobResponseDto
            {
                JobId = job.Id,
                State = job.State,
                Message = $"Bulk status update queued for {dto.TaskIds.Count} tasks"
            });
        }

        var result = await _taskService.BulkUpdateStatusAsync(dto.TaskIds, dto.Status, dto.Reason, employeeId.Value);
        return Ok(result);
    }

    [HttpPatch("assignee")]
    public async Task<IActionResult> BulkUpdateAssignee([FromBody] BulkAssigneeUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        if (dto.TaskIds == null || !dto.TaskIds.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No task IDs provided"));

        if (dto.TaskIds.Count > BulkOperationLimits.AsyncThreshold)
        {
            var job = await _jobService.EnqueueAsync(JobTypes.BulkAssignee, new BulkJobPayload
            {
                TaskIds = dto.TaskIds,
                UserId = employeeId.Value,
                AssigneeId = dto.AssigneeId
            });
            return Accepted(new BulkJobResponseDto { JobId = job.Id, State = job.State, Message = "Bulk assignee update queued" });
        }

        return Ok(await _taskService.BulkUpdateAssigneeAsync(dto.TaskIds, dto.AssigneeId, employeeId.Value));
    }

    [HttpDelete]
    [AdminOnly]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        if (dto.TaskIds == null || !dto.TaskIds.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No task IDs provided"));

        if (dto.TaskIds.Count > BulkOperationLimits.AsyncThreshold)
        {
            var job = await _jobService.EnqueueAsync(JobTypes.BulkDelete, new BulkJobPayload
            {
                TaskIds = dto.TaskIds,
                UserId = employeeId.Value
            });
            return Accepted(new BulkJobResponseDto { JobId = job.Id, State = job.State, Message = "Bulk delete queued" });
        }

        return Ok(await _taskService.BulkDeleteAsync(dto.TaskIds, employeeId.Value));
    }

    [HttpPatch("archive")]
    public async Task<IActionResult> BulkArchive([FromBody] BulkArchiveDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        if (dto.TaskIds == null || !dto.TaskIds.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No task IDs provided"));

        if (dto.TaskIds.Count > BulkOperationLimits.AsyncThreshold)
        {
            var job = await _jobService.EnqueueAsync(JobTypes.BulkArchive, new BulkJobPayload
            {
                TaskIds = dto.TaskIds,
                UserId = employeeId.Value,
                IsArchived = dto.IsArchived
            });
            return Accepted(new BulkJobResponseDto { JobId = job.Id, State = job.State, Message = "Bulk archive queued" });
        }

        return Ok(await _taskService.BulkArchiveAsync(dto.TaskIds, dto.IsArchived, employeeId.Value));
    }

    [HttpPost("tags")]
    public async Task<IActionResult> BulkAddTags([FromBody] BulkTagsDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        if (dto.TaskIds == null || !dto.TaskIds.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No task IDs provided"));
        if (dto.Tags == null || !dto.Tags.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No tags provided"));

        if (dto.TaskIds.Count > BulkOperationLimits.AsyncThreshold)
        {
            var job = await _jobService.EnqueueAsync(JobTypes.BulkAddTags, new BulkJobPayload
            {
                TaskIds = dto.TaskIds,
                UserId = employeeId.Value,
                Tags = dto.Tags
            });
            return Accepted(new BulkJobResponseDto { JobId = job.Id, State = job.State, Message = "Bulk add tags queued" });
        }

        return Ok(await _taskService.BulkAddTagsAsync(dto.TaskIds, dto.Tags, employeeId.Value));
    }

    [HttpDelete("tags")]
    public async Task<IActionResult> BulkRemoveTags([FromBody] BulkTagsDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();
        if (dto.TaskIds == null || !dto.TaskIds.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No task IDs provided"));
        if (dto.Tags == null || !dto.Tags.Any())
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "No tags provided"));

        if (dto.TaskIds.Count > BulkOperationLimits.AsyncThreshold)
        {
            var job = await _jobService.EnqueueAsync(JobTypes.BulkRemoveTags, new BulkJobPayload
            {
                TaskIds = dto.TaskIds,
                UserId = employeeId.Value,
                Tags = dto.Tags
            });
            return Accepted(new BulkJobResponseDto { JobId = job.Id, State = job.State, Message = "Bulk remove tags queued" });
        }

        return Ok(await _taskService.BulkRemoveTagsAsync(dto.TaskIds, dto.Tags, employeeId.Value));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/jobs")]
[Authorize]
public class JobsController : ApiControllerBase
{
    private readonly IJobService _jobService;

    public JobsController(IJobService jobService, IUserRepository userRepository)
        : base(userRepository)
    {
        _jobService = jobService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ListJobs([FromQuery] string? state, [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);
        var (jobs, total) = await _jobService.ListJobsPagedAsync(state, page, limit);
        return Ok(new
        {
            data = jobs.Select(j => new
            {
                j.Id,
                j.Type,
                j.State,
                j.AttemptCount,
                j.LastError,
                j.CreatedAt,
                j.UpdatedAt,
                j.NextAttemptAt
            }),
            page,
            limit,
            totalCount = total
        });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetJob(long id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var job = await _jobService.GetJobAsync(id);
        if (job == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Job not found"));

        return Ok(new
        {
            job.Id,
            job.Type,
            job.State,
            job.AttemptCount,
            job.LastError,
            job.CreatedAt,
            job.UpdatedAt,
            job.NextAttemptAt
        });
    }

    [HttpPost("{id:long}/requeue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Requeue(long id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var ok = await _jobService.RequeueJobAsync(id);
        if (!ok)
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "Job not found or not in DEAD state"));

        var job = await _jobService.GetJobAsync(id);
        return Ok(new
        {
            job!.Id,
            job.State,
            message = "Job requeued"
        });
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Constants;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/analytics")]
[Authorize]
public class AnalyticsController : ApiControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly IJobService _jobs;
    private readonly IAuditService _audit;

    public AnalyticsController(
        IAnalyticsService analytics,
        IJobService jobs,
        IAuditService audit,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _analytics = analytics;
        _jobs = jobs;
        _audit = audit;
    }

    [HttpGet("flow")]
    public Task<IActionResult> Flow(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string basis = MetricBasis.WorkingMinutes,
        [FromQuery] string granularity = "day")
        => Run(async (actorId, role) =>
            await _analytics.GetFlowAsync(departmentId, from, to, basis, granularity, actorId, role));

    [HttpGet("throughput")]
    public Task<IActionResult> Throughput(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string granularity = "day")
        => Run(async (actorId, role) =>
            await _analytics.GetThroughputAsync(departmentId, from, to, granularity, actorId, role));

    [HttpGet("wip")]
    public Task<IActionResult> Wip(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string granularity = "day")
        => Run(async (actorId, role) =>
            await _analytics.GetWipAsync(departmentId, from, to, granularity, actorId, role));

    [HttpGet("sla")]
    public Task<IActionResult> Sla(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string granularity = "day")
        => Run(async (actorId, role) =>
            await _analytics.GetSlaAsync(departmentId, from, to, granularity, actorId, role));

    [HttpGet("quality")]
    public Task<IActionResult> Quality(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
        => Run(async (actorId, role) =>
            await _analytics.GetQualityAsync(departmentId, from, to, actorId, role));

    [HttpGet("workload")]
    public Task<IActionResult> Workload(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] double overheadPercent = 20)
        => Run(async (actorId, role) =>
            await _analytics.GetWorkloadAsync(departmentId, from, to, overheadPercent, actorId, role));

    [HttpGet("compare")]
    public Task<IActionResult> Compare(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string mode = "period",
        [FromQuery] int? otherDepartmentId = null,
        [FromQuery] string basis = MetricBasis.WorkingMinutes)
        => Run(async (actorId, role) =>
            await _analytics.GetCompareAsync(departmentId, from, to, mode, otherDepartmentId, basis, actorId, role));

    [HttpGet("data-quality")]
    public Task<IActionResult> DataQuality(
        [FromQuery] int departmentId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
        => Run(async (actorId, role) =>
            await _analytics.GetDataQualityAsync(departmentId, from, to, actorId, role));

    [HttpGet("me")]
    public Task<IActionResult> Me(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
        => Run(async (actorId, role) =>
            await _analytics.GetMeAsync(from, to, actorId, role));

    [HttpGet("organisation")]
    public Task<IActionResult> Organisation(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string basis = MetricBasis.WorkingMinutes)
        => Run(async (actorId, role) =>
            await _analytics.GetOrganisationAsync(from, to, basis, actorId, role));

    [HttpPost("exports")]
    public async Task<IActionResult> QueueExport([FromBody] AnalyticsExportRequest body)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();
        try
        {
            var export = await _analytics.QueueExportAsync(body, actorId.Value, await GetActorRoleAsync());
            return Accepted(new { exportId = export.Id, jobId = export.JobId, state = export.State });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("exports/{id:long}")]
    public async Task<IActionResult> GetExport(long id)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();
        try
        {
            var export = await _analytics.GetExportAsync(id, actorId.Value, await GetActorRoleAsync());
            if (export == null) return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Export not found"));
            return Ok(new
            {
                export.Id,
                export.Type,
                export.State,
                export.JobId,
                export.FilePath,
                export.RowCount,
                export.Error,
                export.CreatedAt,
                export.CompletedAt,
                downloadReady = export.State == DataExportStates.Succeeded && !string.IsNullOrEmpty(export.FilePath)
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpGet("exports/{id:long}/download")]
    public async Task<IActionResult> DownloadExport(long id)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();
        try
        {
            var export = await _analytics.GetExportAsync(id, actorId.Value, await GetActorRoleAsync());
            if (export == null || export.State != DataExportStates.Succeeded || string.IsNullOrEmpty(export.FilePath))
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Export file not ready"));

            await _audit.LogAsync(
                "EXPORT_DOWNLOADED",
                actorId.Value,
                $"employee:{actorId}",
                "SUCCESS",
                "DataExport",
                export.Id.ToString());

            var bytes = await System.IO.File.ReadAllBytesAsync(export.FilePath);
            return File(bytes, "text/csv", Path.GetFileName(export.FilePath));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("rebuild")]
    public async Task<IActionResult> Rebuild([FromBody] AnalyticsRebuildPayload body)
    {
        var role = await GetActorRoleAsync();
        if (role != global::StaffDesk.Core.Entities.User.Roles.Admin)
            return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden, "Admin only", null));

        if (!DateOnly.TryParse(body.From, out var from) || !DateOnly.TryParse(body.To, out var to))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "from and to (yyyy-MM-dd) required", null));

        var job = await _jobs.EnqueueAsync(JobTypes.MetricRebuild, new AnalyticsRebuildPayload
        {
            From = from.ToString("yyyy-MM-dd"),
            To = to.ToString("yyyy-MM-dd"),
            DepartmentId = body.DepartmentId
        });

        return Accepted(new { jobId = job.Id, type = job.Type });
    }

    private async Task<IActionResult> Run(Func<int, string, Task<object>> action)
    {
        var actorId = await GetCurrentEmployeeIdAsync();
        if (actorId == null) return NoEmployeeLinkError();
        try
        {
            var result = await action(actorId.Value, await GetActorRoleAsync());
            return Ok(result);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    private async Task<string> GetActorRoleAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var userId)) return "Member";
        var user = await UserRepository.GetByIdAsync(userId);
        return user?.Role ?? "Member";
    }
}

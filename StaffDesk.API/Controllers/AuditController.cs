using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/audit")]
[Authorize(Roles = "Admin,Auditor")]
public class AuditController : ApiControllerBase
{
    private readonly IAuditService _auditService;
    private readonly IGovernanceService _governance;

    public AuditController(IAuditService auditService, IGovernanceService governance, IUserRepository userRepository)
        : base(userRepository)
    {
        _auditService = auditService;
        _governance = governance;
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? eventType = null,
        [FromQuery] string? outcome = null,
        [FromQuery] int? actorId = null,
        [FromQuery] string? targetType = null,
        [FromQuery] string? targetId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? sourceIp = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        if (limit > 100) limit = 100;
        if (limit < 1) limit = 1;

        var (items, nextCursor) = await _auditService.QueryAsync(
            eventType, outcome, actorId, targetType, targetId,
            fromDate, toDate, sourceIp, cursor, limit);

        return Ok(new { data = items, nextCursor, limit, count = items.Count() });
    }

    [HttpGet("events/{id}")]
    public async Task<IActionResult> GetEvent(long id)
    {
        var auditEvent = await _auditService.GetByIdAsync(id);
        if (auditEvent == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Audit event not found"));
        return Ok(auditEvent);
    }

    [HttpPost("verify")]
    [Authorize(Roles = "Admin,Auditor")]
    public async Task<IActionResult> VerifyChain([FromBody] VerifyChainRequest request)
    {
        var (isValid, firstBreakIndex) = await _auditService.VerifyChainAsync(request.FromDate, request.ToDate);
        return Ok(new { isValid, firstBreakIndex, fromDate = request.FromDate, toDate = request.ToDate });
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetCount([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var count = await _auditService.GetEventCountAsync(fromDate, toDate);
        return Ok(new { count, fromDate, toDate });
    }

    [HttpPost("exports")]
    public async Task<IActionResult> QueueExport(
        [FromQuery] string? eventType = null,
        [FromQuery] string? outcome = null,
        [FromQuery] int? actorId = null,
        [FromQuery] string? targetType = null,
        [FromQuery] string? targetId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? sourceIp = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return NoEmployeeLinkError();

        var export = await _governance.QueueAuditExportAsync(
            employeeId.Value, eventType, outcome, actorId, targetType, targetId, fromDate, toDate, sourceIp);

        return Accepted(new { export.Id, export.State, export.JobId, export.Type, export.CreatedAt });
    }

    [HttpGet("exports/{id:long}")]
    public async Task<IActionResult> GetExport(long id)
    {
        var export = await _governance.GetAuditExportAsync(id);
        if (export == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Export not found"));

        // AU-15: Auditor may only touch audit-log exports, not subject-access packs.
        if (User.IsInRole("Auditor") && export.Type != "AUDIT")
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Export not found"));

        return Ok(new
        {
            export.Id,
            export.Type,
            export.State,
            export.JobId,
            export.RowCount,
            export.Error,
            export.CreatedAt,
            export.CompletedAt,
            downloadAvailable = export.State == "SUCCEEDED" && !string.IsNullOrEmpty(export.FilePath),
            downloadPath = export.State == "SUCCEEDED" ? $"/v1/audit/exports/{id}/download" : null
        });
    }

    [HttpGet("exports/{id:long}/download")]
    public async Task<IActionResult> DownloadExport(long id)
    {
        var export = await _governance.GetAuditExportAsync(id);
        if (export == null || export.State != "SUCCEEDED" || string.IsNullOrEmpty(export.FilePath))
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Export file not available"));

        if (User.IsInRole("Auditor") && export.Type != "AUDIT")
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Export file not available"));

        if (!System.IO.File.Exists(export.FilePath))
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Export file missing on disk"));

        var bytes = await System.IO.File.ReadAllBytesAsync(export.FilePath);
        return File(bytes, "application/json", Path.GetFileName(export.FilePath));
    }
}

public class VerifyChainRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

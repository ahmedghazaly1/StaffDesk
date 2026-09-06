using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

/// <summary>OB-1: liveness and readiness probes (unauthenticated).</summary>
[ApiController]
[Route("v1/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IOperationsService _operations;

    public HealthController(IOperationsService operations)
    {
        _operations = operations;
    }

    /// <summary>Process is running — no dependency checks.</summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new
        {
            status = "alive",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>Ready to serve traffic — DB, migrations, worker heartbeat.</summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var report = await _operations.GetReadinessAsync(cancellationToken);
        if (!report.Ready)
            return StatusCode(503, report);
        return Ok(report);
    }
}

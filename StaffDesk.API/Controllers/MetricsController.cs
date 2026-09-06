using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

/// <summary>OB-3: Prometheus text exposition format.</summary>
[ApiController]
[Route("v1/metrics")]
[AllowAnonymous]
public class MetricsController : ControllerBase
{
    private readonly IOperationsService _operations;

    public MetricsController(IOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // Refresh gauge values from DB before rendering.
        await _operations.GetSummaryAsync(cancellationToken);
        var body = _operations.RenderPrometheusMetrics();
        return Content(body, "text/plain; version=0.0.4; charset=utf-8");
    }
}

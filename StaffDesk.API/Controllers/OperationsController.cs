using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StaffDesk.API.Common;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;

namespace StaffDesk.API.Controllers;

/// <summary>OB-1/OB-4: admin operations dashboard API.</summary>
[ApiController]
[Route("v1/operations")]
[Authorize(Roles = "Admin")]
public class OperationsController : ApiControllerBase
{
    private readonly IOperationsService _operations;
    private readonly OperationsOptions _options;

    public OperationsController(
        IOperationsService operations,
        IOptions<OperationsOptions> options,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _operations = operations;
        _options = options.Value;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var summary = await _operations.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    /// <summary>OB-4: active alert thresholds and current breach flags.</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(CancellationToken cancellationToken)
    {
        var summary = await _operations.GetSummaryAsync(cancellationToken);

        var alerts = new List<object>();

        if (!summary.Readiness.Ready)
        {
            foreach (var check in summary.Readiness.Checks.Where(c => c.Status != "PASS"))
            {
                alerts.Add(new
                {
                    id = $"readiness.{check.Name}",
                    severity = "critical",
                    message = check.Detail ?? $"{check.Name} check failed",
                    runbook = "docs/runbook.md#readiness-failure"
                });
            }
        }

        if (summary.Jobs.Dead >= _options.DeadJobsWarnCount)
        {
            alerts.Add(new
            {
                id = "jobs.dead",
                severity = "warning",
                message = $"{summary.Jobs.Dead} job(s) in DEAD state",
                threshold = _options.DeadJobsWarnCount,
                runbook = "docs/runbook.md#dead-jobs"
            });
        }

        if (summary.Jobs.OldestQueuedAgeSeconds.HasValue
            && summary.Jobs.OldestQueuedAgeSeconds.Value > _options.JobQueueOldestAgeWarnSeconds)
        {
            alerts.Add(new
            {
                id = "jobs.queue_age",
                severity = "warning",
                message = $"Oldest queued job age {summary.Jobs.OldestQueuedAgeSeconds:F0}s",
                thresholdSeconds = _options.JobQueueOldestAgeWarnSeconds,
                runbook = "docs/runbook.md#queue-backlog"
            });
        }

        if (summary.Rollups.IsStale)
        {
            alerts.Add(new
            {
                id = "rollups.stale",
                severity = "warning",
                message = $"Metric rollup stale ({summary.Rollups.StalenessHours:F1}h)",
                thresholdHours = _options.RollupStalenessWarnHours,
                runbook = "docs/runbook.md#rollup-stale"
            });
        }

        if (summary.Requests.ErrorRatePercent > _options.HttpErrorRateWarnPercent
            && summary.Requests.TotalRequests >= 100)
        {
            alerts.Add(new
            {
                id = "http.error_rate",
                severity = "warning",
                message = $"HTTP 5xx rate {summary.Requests.ErrorRatePercent}%",
                thresholdPercent = _options.HttpErrorRateWarnPercent,
                runbook = "docs/runbook.md#http-errors"
            });
        }

        // OB-4: audit-chain verification failure is immediate and unconditional — no threshold.
        if (!summary.AuditChain.IsValid)
        {
            alerts.Add(new
            {
                id = "audit.chain_broken",
                severity = "critical",
                message = summary.AuditChain.Detail ?? "Audit hash chain verification failed",
                firstBreakIndex = summary.AuditChain.FirstBreakIndex,
                runbook = "docs/runbook.md#audit-chain-failure"
            });
        }

        return Ok(new
        {
            thresholds = _options,
            alerts,
            summary.GeneratedAt
        });
    }
}

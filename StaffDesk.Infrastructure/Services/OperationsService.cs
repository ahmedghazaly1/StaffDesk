using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Services;

public class OperationsService : IOperationsService
{
    private readonly AppDbContext _context;
    private readonly IJobRepository _jobs;
    private readonly IAnalyticsRepository _analytics;
    private readonly IRequestMetricsCollector _metrics;
    private readonly IAuditService _audit;
    private readonly OperationsOptions _options;

    public OperationsService(
        AppDbContext context,
        IJobRepository jobs,
        IAnalyticsRepository analytics,
        IOptions<OperationsOptions> options,
        IRequestMetricsCollector metrics,
        IAuditService audit)
    {
        _context = context;
        _jobs = jobs;
        _analytics = analytics;
        _metrics = metrics;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<ReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<HealthCheckResult>();

        // Database connectivity
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            checks.Add(new HealthCheckResult
            {
                Name = "database",
                Status = canConnect ? "PASS" : "FAIL",
                Detail = canConnect ? "PostgreSQL reachable" : "Cannot connect to PostgreSQL"
            });
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Name = "database",
                Status = "FAIL",
                Detail = ex.Message
            });
        }

        // OB-2: pending EF migrations block readiness
        try
        {
            var pending = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
            var list = pending.ToList();
            checks.Add(new HealthCheckResult
            {
                Name = "migrations",
                Status = list.Count == 0 ? "PASS" : "FAIL",
                Detail = list.Count == 0 ? "Schema up to date" : $"Pending: {string.Join(", ", list)}"
            });
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Name = "migrations",
                Status = "FAIL",
                Detail = ex.Message
            });
        }

        // OB-1: worker heartbeat
        try
        {
            var hb = await _context.WorkerHeartbeats.AsNoTracking().FirstOrDefaultAsync(h => h.Id == 1, cancellationToken);
            if (hb == null)
            {
                checks.Add(new HealthCheckResult
                {
                    Name = "worker",
                    Status = "FAIL",
                    Detail = "No worker heartbeat recorded yet"
                });
            }
            else
            {
                var age = (DateTime.UtcNow - hb.LastSeenAt).TotalSeconds;
                var ok = age <= _options.WorkerHeartbeatMaxAgeSeconds;
                checks.Add(new HealthCheckResult
                {
                    Name = "worker",
                    Status = ok ? "PASS" : "FAIL",
                    Detail = ok
                        ? $"Last seen {age:F0}s ago (instance {hb.InstanceId})"
                        : $"Stale heartbeat ({age:F0}s > {_options.WorkerHeartbeatMaxAgeSeconds}s)"
                });
            }
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheckResult
            {
                Name = "worker",
                Status = "FAIL",
                Detail = ex.Message
            });
        }

        return new ReadinessReport
        {
            Ready = checks.All(c => c.Status == "PASS"),
            Checks = checks
        };
    }

    public async Task<OperationsSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var readiness = await GetReadinessAsync(cancellationToken);
        var counts = await _jobs.CountByStateAsync();
        var oldestAge = await _jobs.GetOldestQueuedAgeSecondsAsync();
        var latestRollup = await _analytics.GetNewestComputedAtAsync();
        double? stalenessHours = latestRollup.HasValue
            ? (DateTime.UtcNow - latestRollup.Value).TotalHours
            : null;

        var jobs = new JobQueueSummary
        {
            Queued = counts.GetValueOrDefault("QUEUED"),
            Running = counts.GetValueOrDefault("RUNNING"),
            Dead = counts.GetValueOrDefault("DEAD"),
            Failed = counts.GetValueOrDefault("FAILED"),
            OldestQueuedAgeSeconds = oldestAge
        };

        var rollups = new RollupSummary
        {
            LatestComputedAt = latestRollup,
            StalenessHours = stalenessHours,
            IsStale = stalenessHours.HasValue && stalenessHours.Value > _options.RollupStalenessWarnHours
        };

        RequestMetricsSummary requests;
        var (total, errors, rate) = _metrics.GetSummary();
        requests = new RequestMetricsSummary
        {
            TotalRequests = total,
            TotalErrors = errors,
            ErrorRatePercent = rate
        };
        _metrics.SetJobQueueDepth(jobs.Queued);
        _metrics.SetOldestQueueAgeSeconds(oldestAge);
        _metrics.SetJobFailures(jobs.Dead);
        _metrics.SetRollupStalenessHours(stalenessHours);

        var auditChain = await VerifyAuditChainAsync(cancellationToken);

        return new OperationsSummary
        {
            Readiness = readiness,
            Jobs = jobs,
            Rollups = rollups,
            Requests = requests,
            AuditChain = auditChain,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>OB-4: any hash-chain break is an immediate, unconditional alert (no threshold).</summary>
    private async Task<AuditChainSummary> VerifyAuditChainAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var from = DateTime.SpecifyKind(DateTime.UtcNow.AddYears(-20), DateTimeKind.Utc);
            var to = DateTime.UtcNow.AddDays(1);
            var (isValid, firstBreak) = await _audit.VerifyChainAsync(from, to);
            return new AuditChainSummary
            {
                IsValid = isValid,
                FirstBreakIndex = firstBreak,
                Detail = isValid
                    ? "Audit hash chain verified"
                    : $"Chain broken at event id {firstBreak}"
            };
        }
        catch (Exception ex)
        {
            return new AuditChainSummary
            {
                IsValid = false,
                Detail = $"Verification failed: {ex.Message}"
            };
        }
    }

    public async Task TouchWorkerHeartbeatAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var hb = await _context.WorkerHeartbeats.FirstOrDefaultAsync(h => h.Id == 1, cancellationToken);
        if (hb == null)
        {
            hb = new WorkerHeartbeat { Id = 1, InstanceId = instanceId, LastSeenAt = DateTime.UtcNow };
            _context.WorkerHeartbeats.Add(hb);
        }
        else
        {
            hb.InstanceId = instanceId;
            hb.LastSeenAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public string RenderPrometheusMetrics() => _metrics.RenderPrometheus();
}

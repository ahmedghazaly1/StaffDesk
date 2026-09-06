namespace StaffDesk.Core.Models;

public sealed class HealthCheckResult
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = ""; // PASS, FAIL, WARN
    public string? Detail { get; set; }
}

public sealed class ReadinessReport
{
    public bool Ready { get; set; }
    public IReadOnlyList<HealthCheckResult> Checks { get; set; } = Array.Empty<HealthCheckResult>();
}

public sealed class OperationsSummary
{
    public ReadinessReport Readiness { get; set; } = new();
    public JobQueueSummary Jobs { get; set; } = new();
    public RollupSummary Rollups { get; set; } = new();
    public RequestMetricsSummary Requests { get; set; } = new();
    public AuditChainSummary AuditChain { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AuditChainSummary
{
    public bool IsValid { get; set; }
    public long? FirstBreakIndex { get; set; }
    public string? Detail { get; set; }
}

public sealed class JobQueueSummary
{
    public int Queued { get; set; }
    public int Running { get; set; }
    public int Dead { get; set; }
    public int Failed { get; set; }
    public double? OldestQueuedAgeSeconds { get; set; }
}

public sealed class RollupSummary
{
    public DateTime? LatestComputedAt { get; set; }
    public double? StalenessHours { get; set; }
    public bool IsStale { get; set; }
}

public sealed class RequestMetricsSummary
{
    public long TotalRequests { get; set; }
    public long TotalErrors { get; set; }
    public double ErrorRatePercent { get; set; }
}

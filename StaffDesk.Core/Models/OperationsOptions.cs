namespace StaffDesk.Core.Models;

/// <summary>OB-4: documented alert thresholds (also in docs/alerts.md).</summary>
public sealed class OperationsOptions
{
    public const string SectionName = "Operations";

    /// <summary>Worker heartbeat older than this marks readiness FAIL (OB-1).</summary>
    public int WorkerHeartbeatMaxAgeSeconds { get; set; } = 90;

    /// <summary>Queued job older than this triggers queue-age alert (OB-4).</summary>
    public int JobQueueOldestAgeWarnSeconds { get; set; } = 300;

    /// <summary>Rollup older than this hours triggers staleness alert (OB-4).</summary>
    public double RollupStalenessWarnHours { get; set; } = 36;

    /// <summary>HTTP 5xx rate above this percent triggers alert (OB-4).</summary>
    public double HttpErrorRateWarnPercent { get; set; } = 1.0;

    /// <summary>Dead-letter jobs at or above this count triggers alert (OB-4).</summary>
    public int DeadJobsWarnCount { get; set; } = 1;
}

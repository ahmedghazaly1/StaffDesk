using StaffDesk.Core.Interfaces;

namespace StaffDesk.Infrastructure.Services;

/// <summary>No-op metrics for Worker process (collector lives in API).</summary>
public sealed class NullRequestMetricsCollector : IRequestMetricsCollector
{
    public void RecordRequest(string route, int statusCode, double durationMs) { }
    public void SetJobQueueDepth(int depth) { }
    public void SetOldestQueueAgeSeconds(double? seconds) { }
    public void SetJobFailures(long deadCount) { }
    public void SetRollupStalenessHours(double? hours) { }
    public string RenderPrometheus() => "# metrics not available in worker\n";
    public (long Total, long Errors, double ErrorRatePercent) GetSummary() => (0, 0, 0);
}

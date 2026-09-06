namespace StaffDesk.Core.Interfaces;

/// <summary>OB-3: in-process request metrics for Prometheus export.</summary>
public interface IRequestMetricsCollector
{
    void RecordRequest(string route, int statusCode, double durationMs);
    void SetJobQueueDepth(int depth);
    void SetOldestQueueAgeSeconds(double? seconds);
    void SetJobFailures(long deadCount);
    void SetRollupStalenessHours(double? hours);
    string RenderPrometheus();
    (long Total, long Errors, double ErrorRatePercent) GetSummary();
}

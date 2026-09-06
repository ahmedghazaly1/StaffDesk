using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Metrics;

public sealed class RequestMetricsCollector : IRequestMetricsCollector
{
    private readonly ConcurrentDictionary<(string Route, int Status), long> _requestCounts = new();
    private readonly ConcurrentDictionary<string, List<double>> _latencies = new();
    private long _totalRequests;
    private long _totalErrors;
    private int _jobQueueDepth;
    private double? _oldestQueueAgeSeconds;
    private long _deadJobs;
    private double? _rollupStalenessHours;

    public void RecordRequest(string route, int statusCode, double durationMs)
    {
        Interlocked.Increment(ref _totalRequests);
        if (statusCode >= 500) Interlocked.Increment(ref _totalErrors);
        _requestCounts.AddOrUpdate((route, statusCode), 1, (_, c) => c + 1);
        var list = _latencies.GetOrAdd(route, _ => new List<double>());
        lock (list)
        {
            list.Add(durationMs);
            if (list.Count > 5000) list.RemoveRange(0, list.Count - 5000);
        }
    }

    public void SetJobQueueDepth(int depth) => _jobQueueDepth = depth;
    public void SetOldestQueueAgeSeconds(double? seconds) => _oldestQueueAgeSeconds = seconds;
    public void SetJobFailures(long deadCount) => _deadJobs = deadCount;
    public void SetRollupStalenessHours(double? hours) => _rollupStalenessHours = hours;

    public (long Total, long Errors, double ErrorRatePercent) GetSummary()
    {
        var total = Interlocked.Read(ref _totalRequests);
        var errors = Interlocked.Read(ref _totalErrors);
        var rate = total == 0 ? 0 : Math.Round(100.0 * errors / total, 2);
        return (total, errors, rate);
    }

    public string RenderPrometheus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# HELP staffdesk_http_requests_total Total HTTP requests by route and status.");
        sb.AppendLine("# TYPE staffdesk_http_requests_total counter");
        foreach (var kv in _requestCounts.OrderBy(k => k.Key.Route).ThenBy(k => k.Key.Status))
        {
            sb.AppendLine($"staffdesk_http_requests_total{{route=\"{Escape(kv.Key.Route)}\",status=\"{kv.Key.Status}\"}} {kv.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine("# HELP staffdesk_http_request_duration_ms Request duration histogram (p50 from sample).");
        sb.AppendLine("# TYPE staffdesk_http_request_duration_ms summary");
        foreach (var kv in _latencies)
        {
            double p50;
            lock (kv.Value)
            {
                if (kv.Value.Count == 0) continue;
                var sorted = kv.Value.OrderBy(x => x).ToList();
                p50 = sorted[sorted.Count / 2];
            }
            sb.AppendLine($"staffdesk_http_request_duration_ms{{route=\"{Escape(kv.Key)}\",quantile=\"0.5\"}} {p50.ToString(CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine("# HELP staffdesk_job_queue_depth Queued jobs waiting to run.");
        sb.AppendLine("# TYPE staffdesk_job_queue_depth gauge");
        sb.AppendLine($"staffdesk_job_queue_depth {_jobQueueDepth.ToString(CultureInfo.InvariantCulture)}");

        if (_oldestQueueAgeSeconds.HasValue)
        {
            sb.AppendLine("# HELP staffdesk_job_queue_oldest_age_seconds Age of oldest queued job.");
            sb.AppendLine("# TYPE staffdesk_job_queue_oldest_age_seconds gauge");
            sb.AppendLine($"staffdesk_job_queue_oldest_age_seconds {_oldestQueueAgeSeconds.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine("# HELP staffdesk_job_dead_total Jobs in DEAD state.");
        sb.AppendLine("# TYPE staffdesk_job_dead_total gauge");
        sb.AppendLine($"staffdesk_job_dead_total {_deadJobs.ToString(CultureInfo.InvariantCulture)}");

        if (_rollupStalenessHours.HasValue)
        {
            sb.AppendLine("# HELP staffdesk_rollup_staleness_hours Hours since latest metric rollup.");
            sb.AppendLine("# TYPE staffdesk_rollup_staleness_hours gauge");
            sb.AppendLine($"staffdesk_rollup_staleness_hours {_rollupStalenessHours.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

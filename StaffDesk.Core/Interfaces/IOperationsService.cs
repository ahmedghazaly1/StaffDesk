using StaffDesk.Core.Models;

namespace StaffDesk.Core.Interfaces;

public interface IOperationsService
{
    Task<ReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default);
    Task<OperationsSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task TouchWorkerHeartbeatAsync(string instanceId, CancellationToken cancellationToken = default);
    string RenderPrometheusMetrics();
}

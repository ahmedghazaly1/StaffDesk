using StaffDesk.Core.Entities;
using StaffDesk.Core.Models;

namespace StaffDesk.Core.Interfaces;

public interface IAnalyticsRepository
{
    Task<DailyMetricSnapshot> InsertSnapshotAsync(DailyMetricSnapshot snapshot);
    Task<int> GetLatestVersionAsync(int departmentId, DateOnly date);
    Task<DailyMetricSnapshot?> GetLatestAsync(int departmentId, DateOnly date);
    Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeAsync(int departmentId, DateOnly from, DateOnly to);
    Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeForDepartmentsAsync(
        IEnumerable<int> departmentIds, DateOnly from, DateOnly to);
    Task<DateTime?> GetNewestComputedAtAsync(int? departmentId = null);

    // Transactional reads used ONLY by rollup/rebuild (not by GET analytics endpoints).
    Task<IReadOnlyList<WorkTask>> GetTasksForDepartmentAsync(int departmentId);
    Task<IReadOnlyList<TaskStatusInterval>> GetIntervalsForTasksAsync(IEnumerable<int> taskIds);
    Task<IReadOnlyList<TaskRequest>> GetRequestsForDepartmentAsync(int departmentId);
    Task<IReadOnlyList<ReworkEvent>> GetReworkEventsForTasksAsync(IEnumerable<int> taskIds);
    Task<IReadOnlyList<TaskActivity>> GetAssignmentActivitiesForTasksAsync(IEnumerable<int> taskIds);
    Task<IReadOnlyList<int>> GetActiveDepartmentIdsAsync();
}

public interface IAnalyticsService
{
    Task<DailyMetricSnapshot> RollupDepartmentDayAsync(int departmentId, DateOnly date);
    Task<int> RebuildAsync(DateOnly from, DateOnly to, int? departmentId = null);

    Task<object> GetFlowAsync(int departmentId, DateOnly from, DateOnly to, string basis, string granularity, int actorEmployeeId, string actorRole);
    Task<object> GetThroughputAsync(int departmentId, DateOnly from, DateOnly to, string granularity, int actorEmployeeId, string actorRole);
    Task<object> GetWipAsync(int departmentId, DateOnly from, DateOnly to, string granularity, int actorEmployeeId, string actorRole);
    Task<object> GetSlaAsync(int departmentId, DateOnly from, DateOnly to, string granularity, int actorEmployeeId, string actorRole);
    Task<object> GetQualityAsync(int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole);
    Task<object> GetWorkloadAsync(int departmentId, DateOnly from, DateOnly to, double overheadPercent, int actorEmployeeId, string actorRole);
    Task<object> GetCompareAsync(int departmentId, DateOnly from, DateOnly to, string mode, int? otherDepartmentId, string basis, int actorEmployeeId, string actorRole);
    Task<object> GetDataQualityAsync(int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole);
    Task<object> GetMeAsync(DateOnly from, DateOnly to, int actorEmployeeId, string actorRole);
    Task<object> GetOrganisationAsync(DateOnly from, DateOnly to, string basis, int actorEmployeeId, string actorRole);

    Task<DataExport> QueueExportAsync(AnalyticsExportRequest request, int actorEmployeeId, string actorRole);
    Task<DataExport?> GetExportAsync(long exportId, int actorEmployeeId, string actorRole);
    Task ProcessAnalyticsExportAsync(long exportId);

    PercentileResult ComputeLeadTimes(IEnumerable<(DateTime CreatedAt, DateTime CompletedAt, int DepartmentId)> tasks, string basis);
    PercentileResult ComputeCycleTimes(
        IEnumerable<(DateTime? FirstInProgressAt, DateTime CompletedAt, int DepartmentId, bool IncludesBackfilled)> tasks,
        string basis);
}

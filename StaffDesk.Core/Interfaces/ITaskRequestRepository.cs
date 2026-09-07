using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ITaskRequestRepository
{
    Task<TaskRequest?> GetByIdAsync(int id);
    Task<TaskRequest> CreateAsync(TaskRequest request);
    Task<TaskRequest> UpdateAsync(TaskRequest request);

    // "My requests" list: only requests the viewer submitted. Department-wide intake belongs on
    // the triage queue, not this list — even for managers and admins.
    Task<(IEnumerable<TaskRequest> Items, int TotalCount)> GetFilteredAsync(
        int viewerId,
        int? departmentId = null,
        string? status = null,
        int? page = null,
        int? limit = null,
        string? sort = "-submittedAt"
    );

    // Requests awaiting a triage decision for a department, oldest first (WC-8). No visibility
    // filtering applied here - the service layer restricts the caller to triage authorities first.
    Task<List<TaskRequest>> GetTriageQueueAsync(int departmentId);

    Task<List<TaskRequest>> GetDecidedForMetricsAsync(int? departmentId, DateTime? fromDate, DateTime? toDate);

    Task ExecuteInTransactionAsync(Func<Task> operation);
}

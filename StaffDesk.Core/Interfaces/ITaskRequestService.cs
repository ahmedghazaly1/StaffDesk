using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ITaskRequestService
{
    // WC-2: any authenticated employee may raise a request against ANY department.
    Task<TaskRequest> SubmitAsync(
        int requestedById,
        string title,
        string? description,
        int departmentId,
        string? businessJustification,
        DateTime? desiredByDate
    );

    Task<TaskRequest?> GetRequestAsync(int id, int viewerId);

    /// <summary>Requests this employee submitted. Department intake is on the triage queue.</summary>
    Task<(IEnumerable<TaskRequest> Items, int TotalCount)> GetListAsync(
        int viewerId,
        int? departmentId = null,
        string? status = null,
        int? page = null,
        int? limit = null
    );

    // WC-8: requests awaiting a triage decision for a department, oldest first.
    /// <summary>WC-8: departmentId is optional - when omitted, auto-resolves to every department
    /// the caller has triage authority over (as manager or designated triager). A caller with no
    /// managed department and no explicit departmentId gets a 400, not a 403.</summary>
    Task<List<TaskRequest>> GetTriageQueueAsync(int? departmentId, int actorId);

    // WC-6/WC-7: triager (department manager or Admin) accepts -> creates a linked Task.
    Task<TaskRequest> AcceptAsync(int requestId, int actorId);

    // WC-4/WC-7: triager declines - requires a reason from the fixed taxonomy plus a free-text note.
    Task<TaskRequest> DeclineAsync(int requestId, int actorId, string reasonCategory, string note);

    // WC-5/WC-7: triager marks the request as merged into another request.
    Task<TaskRequest> MergeAsync(int requestId, int actorId, int mergedIntoRequestId);

    Task<bool> IsTriageAuthorityAsync(int actorId, int departmentId);

    Task<TriageLatencyMetrics> GetTriageLatencyMetricsAsync(int? departmentId, DateTime? fromDate, DateTime? toDate);
}

public class TriageLatencyMetrics
{
    public int TotalDecided { get; set; }
    public double AverageMinutes { get; set; }
    public double MedianMinutes { get; set; }
    public double P95Minutes { get; set; }
}

using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IGovernanceService
{
    Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync();

    Task<LegalHold> PlaceLegalHoldAsync(string targetType, string targetId, string reason, int actorEmployeeId);
    Task<LegalHold> LiftLegalHoldAsync(long holdId, int actorEmployeeId);
    Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync();

    Task<DataExport> QueueAuditExportAsync(
        int actorEmployeeId,
        string? eventType,
        string? outcome,
        int? actorId,
        string? targetType,
        string? targetId,
        DateTime? fromDate,
        DateTime? toDate,
        string? sourceIp);

    Task<DataExport?> GetAuditExportAsync(long id);
    Task<DataExport> QueueSubjectAccessExportAsync(int subjectEmployeeId, int actorEmployeeId);
    Task<(long JobId, long PurgeRunId)> QueuePurgeAsync(int actorEmployeeId, bool dryRun);
    Task<PurgeRun?> GetPurgeRunAsync(long id);
    Task<(long JobId, string Message)> QueueErasureAsync(int subjectEmployeeId, int actorEmployeeId);

    // Job handlers
    Task ProcessAuditExportAsync(long exportId);
    Task ProcessSubjectAccessExportAsync(long exportId);
    Task ProcessPurgeAsync(long purgeRunId);
    Task ProcessErasureAsync(int subjectEmployeeId, int actorEmployeeId);
}

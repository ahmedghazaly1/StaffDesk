using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IGovernanceRepository
{
    Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync();
    Task SeedRetentionPoliciesIfEmptyAsync(IEnumerable<RetentionPolicy> defaults);

    Task<LegalHold> PlaceHoldAsync(LegalHold hold);
    Task<LegalHold?> GetHoldByIdAsync(long id);
    Task<LegalHold?> LiftHoldAsync(long id, int liftedById);
    Task<IReadOnlyList<LegalHold>> GetActiveHoldsAsync(string? targetType = null, string? targetId = null);
    Task<bool> HasActiveHoldAsync(string targetType, string targetId);

    Task<DataExport> CreateExportAsync(DataExport export);
    Task<DataExport?> GetExportByIdAsync(long id);
    Task UpdateExportAsync(DataExport export);

    Task<PurgeRun> CreatePurgeRunAsync(PurgeRun run);
    Task<PurgeRun?> GetPurgeRunByIdAsync(long id);
    Task UpdatePurgeRunAsync(PurgeRun run);
}

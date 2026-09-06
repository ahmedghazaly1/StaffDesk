using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class GovernanceRepository : IGovernanceRepository
{
    private readonly AppDbContext _context;

    public GovernanceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync()
    {
        return await _context.RetentionPolicies
            .AsNoTracking()
            .OrderBy(p => p.DataClass)
            .ToListAsync();
    }

    public async Task SeedRetentionPoliciesIfEmptyAsync(IEnumerable<RetentionPolicy> defaults)
    {
        if (await _context.RetentionPolicies.AnyAsync()) return;
        _context.RetentionPolicies.AddRange(defaults);
        await _context.SaveChangesAsync();
    }

    public async Task<LegalHold> PlaceHoldAsync(LegalHold hold)
    {
        _context.LegalHolds.Add(hold);
        await _context.SaveChangesAsync();
        return hold;
    }

    public Task<LegalHold?> GetHoldByIdAsync(long id) =>
        _context.LegalHolds.FirstOrDefaultAsync(h => h.Id == id);

    public async Task<LegalHold?> LiftHoldAsync(long id, int liftedById)
    {
        var hold = await _context.LegalHolds.FirstOrDefaultAsync(h => h.Id == id);
        if (hold == null || hold.LiftedAt != null) return hold;
        hold.LiftedAt = DateTime.UtcNow;
        hold.LiftedById = liftedById;
        await _context.SaveChangesAsync();
        return hold;
    }

    public async Task<IReadOnlyList<LegalHold>> GetActiveHoldsAsync(string? targetType = null, string? targetId = null)
    {
        var q = _context.LegalHolds.AsNoTracking().Where(h => h.LiftedAt == null);
        if (!string.IsNullOrWhiteSpace(targetType))
            q = q.Where(h => h.TargetType == targetType);
        if (!string.IsNullOrWhiteSpace(targetId))
            q = q.Where(h => h.TargetId == targetId);
        return await q.OrderByDescending(h => h.PlacedAt).ToListAsync();
    }

    public async Task<bool> HasActiveHoldAsync(string targetType, string targetId)
    {
        return await _context.LegalHolds.AnyAsync(h =>
            h.LiftedAt == null && h.TargetType == targetType && h.TargetId == targetId);
    }

    public async Task<DataExport> CreateExportAsync(DataExport export)
    {
        _context.DataExports.Add(export);
        await _context.SaveChangesAsync();
        return export;
    }

    public Task<DataExport?> GetExportByIdAsync(long id) =>
        _context.DataExports.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

    public async Task UpdateExportAsync(DataExport export)
    {
        var tracked = await _context.DataExports.FirstOrDefaultAsync(e => e.Id == export.Id);
        if (tracked == null) return;
        tracked.State = export.State;
        tracked.JobId = export.JobId;
        tracked.FilePath = export.FilePath;
        tracked.RowCount = export.RowCount;
        tracked.Error = export.Error;
        tracked.CompletedAt = export.CompletedAt;
        tracked.FilterJson = export.FilterJson;
        await _context.SaveChangesAsync();
    }

    public async Task<PurgeRun> CreatePurgeRunAsync(PurgeRun run)
    {
        _context.PurgeRuns.Add(run);
        await _context.SaveChangesAsync();
        return run;
    }

    public Task<PurgeRun?> GetPurgeRunByIdAsync(long id) =>
        _context.PurgeRuns.FirstOrDefaultAsync(r => r.Id == id);

    public async Task UpdatePurgeRunAsync(PurgeRun run)
    {
        _context.PurgeRuns.Update(run);
        await _context.SaveChangesAsync();
    }
}

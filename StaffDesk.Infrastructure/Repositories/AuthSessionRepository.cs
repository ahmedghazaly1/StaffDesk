using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class AuthSessionRepository : IAuthSessionRepository
{
    private readonly AppDbContext _context;

    public AuthSessionRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(AuthSession session)
    {
        _context.AuthSessions.Add(session);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AuthSession session)
    {
        await _context.SaveChangesAsync();
    }

    public Task<AuthSession?> GetByIdAsync(Guid id) =>
        _context.AuthSessions.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);

    public Task<AuthSession?> GetByRefreshHashAsync(string refreshTokenHash) =>
        _context.AuthSessions.Include(s => s.User).FirstOrDefaultAsync(s =>
            s.RefreshTokenHash == refreshTokenHash || s.PreviousRefreshTokenHash == refreshTokenHash);

    public async Task<IReadOnlyList<AuthSession>> ListActiveByUserAsync(int userId) =>
        await _context.AuthSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync();

    public async Task RevokeFamilyAsync(Guid familyId, string reason)
    {
        // Load-then-update so InMemory (tests) and relational providers both work.
        var now = DateTime.UtcNow;
        var rows = await _context.AuthSessions
            .Where(s => s.FamilyId == familyId && s.RevokedAt == null)
            .ToListAsync();
        foreach (var s in rows)
        {
            s.RevokedAt = now;
            s.RevokedReason = reason;
        }
        if (rows.Count > 0)
            await _context.SaveChangesAsync();
    }

    public async Task RevokeAllForUserAsync(int userId, string reason)
    {
        var now = DateTime.UtcNow;
        var rows = await _context.AuthSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync();
        foreach (var s in rows)
        {
            s.RevokedAt = now;
            s.RevokedReason = reason;
        }
        if (rows.Count > 0)
            await _context.SaveChangesAsync();
    }

    public async Task<int> PurgeExpiredAsync(DateTime cutoffUtc, int batchSize)
    {
        var rows = await _context.AuthSessions
            .Where(s => s.RefreshExpiresAt < cutoffUtc || (s.RevokedAt != null && s.RevokedAt < cutoffUtc))
            .OrderBy(s => s.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
        if (rows.Count == 0) return 0;
        _context.AuthSessions.RemoveRange(rows);
        await _context.SaveChangesAsync();
        return rows.Count;
    }
}

public class LoginThrottleRepository : ILoginThrottleRepository
{
    private readonly AppDbContext _context;

    public LoginThrottleRepository(AppDbContext context) => _context = context;

    public async Task<LoginThrottle> GetOrCreateAsync(string key)
    {
        var row = await _context.LoginThrottles.FirstOrDefaultAsync(t => t.Key == key);
        if (row != null) return row;
        row = new LoginThrottle { Key = key, FailureCount = 0, UpdatedAt = DateTime.UtcNow };
        _context.LoginThrottles.Add(row);
        await _context.SaveChangesAsync();
        return row;
    }

    public Task UpdateAsync(LoginThrottle row)
    {
        row.UpdatedAt = DateTime.UtcNow;
        return _context.SaveChangesAsync();
    }
}

using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IAuthSessionRepository
{
    Task AddAsync(AuthSession session);
    Task UpdateAsync(AuthSession session);
    Task<AuthSession?> GetByIdAsync(Guid id);
    Task<AuthSession?> GetByRefreshHashAsync(string refreshTokenHash);
    Task<IReadOnlyList<AuthSession>> ListActiveByUserAsync(int userId);
    Task RevokeFamilyAsync(Guid familyId, string reason);
    Task RevokeAllForUserAsync(int userId, string reason);
    Task<int> PurgeExpiredAsync(DateTime cutoffUtc, int batchSize);
}

public interface ILoginThrottleRepository
{
    Task<LoginThrottle> GetOrCreateAsync(string key);
    Task UpdateAsync(LoginThrottle row);
}

public interface IAuthSessionService
{
    Task<(string AccessToken, string RefreshToken, DateTime AccessExpiresAt, AuthSession Session)> IssueAsync(
        User user, string? ip, string? userAgent);
    Task<(string AccessToken, string RefreshToken, DateTime AccessExpiresAt)?> RotateAsync(
        string refreshToken, string? ip, string? userAgent);
    Task<bool> IsSessionActiveAsync(Guid sessionId);
    Task TouchAsync(Guid sessionId);
    Task<IReadOnlyList<AuthSession>> ListMineAsync(int userId);
    Task<bool> RevokeOwnAsync(int userId, Guid sessionId);
    Task<int> RevokeAllForEmployeeAsync(int employeeId, string reason);
    Task<int> RevokeAllForUserAsync(int userId, string reason);
    Task RevokeCurrentAsync(Guid sessionId, string reason);
}

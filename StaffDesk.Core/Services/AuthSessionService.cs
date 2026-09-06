using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;

namespace StaffDesk.Core.Services;

public class AuthSessionService : IAuthSessionService
{
    private readonly IAuthSessionRepository _sessions;
    private readonly IUserRepository _users;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;
    private readonly AuthOptions _options;

    public AuthSessionService(
        IAuthSessionRepository sessions,
        IUserRepository users,
        IJwtService jwt,
        IAuditService audit,
        IConfiguration configuration)
    {
        _sessions = sessions;
        _users = users;
        _jwt = jwt;
        _audit = audit;
        _options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime AccessExpiresAt, AuthSession Session)> IssueAsync(
        User user, string? ip, string? userAgent)
    {
        var refresh = CreateRefreshToken();
        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenHash = Hash(refresh),
            RefreshExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Ip = ip,
            UserAgent = Truncate(userAgent, 500)
        };
        await _sessions.AddAsync(session);
        var access = _jwt.GenerateToken(user, session.Id, out var expires);
        return (access, refresh, expires, session);
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime AccessExpiresAt)?> RotateAsync(
        string refreshToken, string? ip, string? userAgent)
    {
        var hash = Hash(refreshToken);
        var existing = await _sessions.GetByRefreshHashAsync(hash);
        if (existing == null)
            return null;

        var isPrevious = !string.Equals(existing.RefreshTokenHash, hash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.PreviousRefreshTokenHash, hash, StringComparison.OrdinalIgnoreCase);

        if (existing.RevokedAt != null || isPrevious)
        {
            await _sessions.RevokeFamilyAsync(existing.FamilyId, "refresh_reuse");
            await _audit.LogAsync(
                "TOKEN_REUSE_DETECTED",
                existing.User?.EmployeeId,
                existing.User != null ? $"{existing.User.Username} ({existing.User.Role})" : "unknown",
                "DENIED",
                "AuthSession",
                existing.FamilyId.ToString(),
                sourceIp: ip,
                userAgent: userAgent,
                changes: new { existing.Id, existing.FamilyId });
            return null;
        }

        if (existing.RefreshExpiresAt < DateTime.UtcNow)
        {
            existing.RevokedAt = DateTime.UtcNow;
            existing.RevokedReason = "expired";
            await _sessions.UpdateAsync(existing);
            return null;
        }

        var user = existing.User ?? await _users.GetByIdAsync(existing.UserId);
        if (user == null) return null;

        var refresh = CreateRefreshToken();
        existing.PreviousRefreshTokenHash = existing.RefreshTokenHash;
        existing.RefreshTokenHash = Hash(refresh);
        existing.RefreshExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        existing.LastSeenAt = DateTime.UtcNow;
        existing.Ip = ip ?? existing.Ip;
        existing.UserAgent = Truncate(userAgent, 500) ?? existing.UserAgent;
        existing.Replaced = true;
        await _sessions.UpdateAsync(existing);

        var access = _jwt.GenerateToken(user, existing.Id, out var expires);
        return (access, refresh, expires);
    }

    public async Task<bool> IsSessionActiveAsync(Guid sessionId)
    {
        var session = await _sessions.GetByIdAsync(sessionId);
        return session != null && session.RevokedAt == null && session.RefreshExpiresAt >= DateTime.UtcNow;
    }

    public async Task TouchAsync(Guid sessionId)
    {
        var session = await _sessions.GetByIdAsync(sessionId);
        if (session == null || session.RevokedAt != null) return;
        session.LastSeenAt = DateTime.UtcNow;
        await _sessions.UpdateAsync(session);
    }

    public Task<IReadOnlyList<AuthSession>> ListMineAsync(int userId) =>
        _sessions.ListActiveByUserAsync(userId);

    public async Task<bool> RevokeOwnAsync(int userId, Guid sessionId)
    {
        var session = await _sessions.GetByIdAsync(sessionId);
        if (session == null || session.UserId != userId || session.RevokedAt != null)
            return false;
        session.RevokedAt = DateTime.UtcNow;
        session.RevokedReason = "user_revoked";
        await _sessions.UpdateAsync(session);
        return true;
    }

    public async Task<int> RevokeAllForEmployeeAsync(int employeeId, string reason)
    {
        var user = await _users.GetByEmployeeIdAsync(employeeId);
        if (user == null) return 0;
        return await RevokeAllForUserAsync(user.Id, reason);
    }

    public async Task<int> RevokeAllForUserAsync(int userId, string reason)
    {
        await _sessions.RevokeAllForUserAsync(userId, reason);
        return 1;
    }

    public async Task RevokeCurrentAsync(Guid sessionId, string reason)
    {
        var session = await _sessions.GetByIdAsync(sessionId);
        if (session == null || session.RevokedAt != null) return;
        session.RevokedAt = DateTime.UtcNow;
        session.RevokedReason = reason;
        await _sessions.UpdateAsync(session);
    }

    public static string Hash(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }

    private static string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);
}

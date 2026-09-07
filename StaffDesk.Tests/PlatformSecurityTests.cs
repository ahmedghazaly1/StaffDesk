using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using StaffDesk.Core;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;

namespace StaffDesk.Tests;

public class AuthSessionTests
{
    [Fact]
    public async Task Refresh_Reuse_Revokes_Family()
    {
        var user = new User { Id = 1, Username = "u", PasswordHash = "x", Email = "u@t", Role = User.Roles.Member };
        var sessions = new FakeSessions();
        var users = new StubUsers(user);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "LocalDev-StaffDesk-Signing-Secret-32ch",
            ["JwtSettings:Issuer"] = "StaffDesk",
            ["JwtSettings:Audience"] = "StaffDeskUsers",
            ["JwtSettings:ExpiryMinutes"] = "15",
            ["Auth:RefreshTokenDays"] = "14"
        }).Build();

        var jwt = new JwtService(config);
        var audit = new RecordingAudit();
        var svc = new AuthSessionService(sessions, users, jwt, audit, config);

        var issued = await svc.IssueAsync(user, "127.0.0.1", "test");
        var firstRefresh = issued.RefreshToken;
        var rotated = await svc.RotateAsync(firstRefresh, "127.0.0.1", "test");
        Assert.NotNull(rotated);

        var reused = await svc.RotateAsync(firstRefresh, "127.0.0.1", "test");
        Assert.Null(reused);
        Assert.Contains(audit.Types, t => t == "TOKEN_REUSE_DETECTED");
        Assert.False(await svc.IsSessionActiveAsync(issued.Session.Id));
    }

    private sealed class StubUsers : IUserRepository
    {
        private readonly User _user;
        public StubUsers(User user) => _user = user;
        public Task<User?> GetByUsernameAsync(string username) => Task.FromResult<User?>(_user);
        public Task<User> CreateAsync(User user) => Task.FromResult(user);
        public Task<bool> UserExistsAsync(string username) => Task.FromResult(false);
        public Task<User?> GetByIdAsync(int id) => Task.FromResult<User?>(_user);
        public Task<User?> GetByEmployeeIdAsync(int employeeId) => Task.FromResult<User?>(_user);
        public Task<IReadOnlyList<int>> GetEmployeeIdsByRoleAsync(string role) =>
            Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task UpdateAsync(User user) => Task.CompletedTask;
    }

    private sealed class FakeSessions : IAuthSessionRepository
    {
        private readonly List<AuthSession> _rows = new();

        public Task AddAsync(AuthSession session)
        {
            session.User ??= new User { Id = session.UserId };
            _rows.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AuthSession session) => Task.CompletedTask;

        public Task<AuthSession?> GetByIdAsync(Guid id) =>
            Task.FromResult(_rows.FirstOrDefault(s => s.Id == id));

        public Task<AuthSession?> GetByRefreshHashAsync(string refreshTokenHash) =>
            Task.FromResult(_rows.FirstOrDefault(s =>
                s.RefreshTokenHash == refreshTokenHash || s.PreviousRefreshTokenHash == refreshTokenHash));

        public Task<IReadOnlyList<AuthSession>> ListActiveByUserAsync(int userId) =>
            Task.FromResult<IReadOnlyList<AuthSession>>(_rows.Where(s => s.UserId == userId && s.RevokedAt == null).ToList());

        public Task RevokeFamilyAsync(Guid familyId, string reason)
        {
            var now = DateTime.UtcNow;
            foreach (var s in _rows.Where(s => s.FamilyId == familyId && s.RevokedAt == null))
            {
                s.RevokedAt = now;
                s.RevokedReason = reason;
            }
            return Task.CompletedTask;
        }

        public Task RevokeAllForUserAsync(int userId, string reason)
        {
            var now = DateTime.UtcNow;
            foreach (var s in _rows.Where(s => s.UserId == userId && s.RevokedAt == null))
            {
                s.RevokedAt = now;
                s.RevokedReason = reason;
            }
            return Task.CompletedTask;
        }

        public Task<int> PurgeExpiredAsync(DateTime cutoffUtc, int batchSize) => Task.FromResult(0);
    }

    private sealed class RecordingAudit : IAuditService
    {
        public List<string> Types { get; } = new();
        public Task<AuditEvent> LogAsync(string eventType, int? actorId, string actorLabel, string outcome, string? targetType = null, string? targetId = null, int? onBehalfOfId = null, object? changes = null, string? requestId = null, string? sourceIp = null, string? userAgent = null)
        {
            Types.Add(eventType);
            return Task.FromResult(new AuditEvent { EventType = eventType });
        }
        public Task<AuditEvent?> GetByIdAsync(long id) => Task.FromResult<AuditEvent?>(null);
        public Task<(IEnumerable<AuditEvent> Items, string? NextCursor)> QueryAsync(string? eventType = null, string? outcome = null, int? actorId = null, string? targetType = null, string? targetId = null, DateTime? fromDate = null, DateTime? toDate = null, string? sourceIp = null, string? cursor = null, int limit = 50) =>
            Task.FromResult<(IEnumerable<AuditEvent>, string?)>((Array.Empty<AuditEvent>(), null));
        public Task<(bool IsValid, long? FirstBreakIndex)> VerifyChainAsync(DateTime fromDate, DateTime toDate) => Task.FromResult((true, (long?)null));
        public Task<long> GetEventCountAsync(DateTime? fromDate = null, DateTime? toDate = null) => Task.FromResult(0L);
    }
}

public class UrlAllowListTests
{
    [Fact]
    public void Https_Is_Allowed() => Assert.True(UrlAllowList.IsAllowed("https://hooks.example.com/x"));

    [Fact]
    public void Javascript_Is_Rejected() => Assert.False(UrlAllowList.IsAllowed("javascript:alert(1)"));

    [Fact]
    public void Http_Loopback_Is_Allowed() => Assert.True(UrlAllowList.IsAllowed("http://localhost:8080/hook"));

    [Fact]
    public void Http_Public_Is_Rejected() => Assert.False(UrlAllowList.IsAllowed("http://evil.example/hook"));
}

public class ConcurrencyHelperTests
{
    [Fact]
    public void ComputeETag_Is_Stable_For_Same_Timestamp()
    {
        var t = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(StaffDesk.API.Common.ConcurrencyHelper.ComputeETag(t), StaffDesk.API.Common.ConcurrencyHelper.ComputeETag(t));
    }

    [Fact]
    public void RequireIfMatch_Missing_Is_428_Stale_Is_412()
    {
        var t = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
        var ctx = new DefaultHttpContext();
        var missing = StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(ctx.Request, t);
        Assert.NotNull(missing);
        Assert.Equal(428, missing.Value.Status);

        ctx.Request.Headers.IfMatch = "\"deadbeef\"";
        var stale = StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(ctx.Request, t);
        Assert.NotNull(stale);
        Assert.Equal(412, stale.Value.Status);

        ctx.Request.Headers.IfMatch = StaffDesk.API.Common.ConcurrencyHelper.ComputeETag(t);
        Assert.Null(StaffDesk.API.Common.ConcurrencyHelper.RequireIfMatch(ctx.Request, t));
    }
}

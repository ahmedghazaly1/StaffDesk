using Microsoft.Extensions.Configuration;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;

namespace StaffDesk.Core.Services;

public class AuthService : IAuthService
{
    public const string InvalidCredentials = "Invalid username or password";

    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAuthSessionService _sessions;
    private readonly ILoginThrottleRepository _throttles;
    private readonly IJwtService _jwtService;
    private readonly IAuditService _audit;
    private readonly AuthOptions _options;

    public AuthService(
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository,
        IAuthSessionService sessions,
        ILoginThrottleRepository throttles,
        IJwtService jwtService,
        IAuditService audit,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
        _sessions = sessions;
        _throttles = throttles;
        _jwtService = jwtService;
        _audit = audit;
        _options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
    }

    public async Task<(bool Success, string? AccessToken, string? RefreshToken, DateTime? ExpiresAt, string? Role, string? Error)> LoginAsync(
        string username, string password, string? ip, string? userAgent)
    {
        var userKey = "user:" + (username ?? "").Trim().ToLowerInvariant();
        var ipKey = "ip:" + (string.IsNullOrWhiteSpace(ip) ? "unknown" : ip);

        if (await IsLockedAsync(userKey) || await IsLockedAsync(ipKey))
        {
            await ApplyProgressiveDelayAsync(userKey);
            return (false, null, null, null, null, InvalidCredentials);
        }

        var user = await _userRepository.GetByUsernameAsync(username);
        var passwordOk = user != null && _jwtService.VerifyPassword(password, user.PasswordHash);

        if (!passwordOk)
        {
            await RegisterFailureAsync(userKey, username, ip, userAgent);
            await RegisterFailureAsync(ipKey, username, ip, userAgent);
            await ApplyProgressiveDelayAsync(userKey);
            return (false, null, null, null, null, InvalidCredentials);
        }

        if (user!.EmployeeId.HasValue)
        {
            var employee = await _employeeRepository.GetByIdAsync(user.EmployeeId.Value);
            if (employee != null && !employee.IsActive)
            {
                await ApplyProgressiveDelayAsync(userKey);
                return (false, null, null, null, null, InvalidCredentials);
            }
        }

        await ResetThrottleAsync(userKey);

        var issued = await _sessions.IssueAsync(user, ip, userAgent);
        return (true, issued.AccessToken, issued.RefreshToken, issued.AccessExpiresAt, user.Role, null);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return (false, InvalidCredentials);
        if (!_jwtService.VerifyPassword(currentPassword, user.PasswordHash))
            return (false, InvalidCredentials);
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return (false, "Password must be at least 8 characters");

        user.PasswordHash = _jwtService.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        await _sessions.RevokeAllForUserAsync(userId, "password_change");
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangeRoleAsync(int targetUserId, string newRole, int actorUserId)
    {
        var allowed = new[] { User.Roles.Admin, User.Roles.Manager, User.Roles.Member, User.Roles.Auditor, User.Roles.HrAdmin };
        if (!allowed.Contains(newRole))
            return (false, "Unknown role");

        var user = await _userRepository.GetByIdAsync(targetUserId);
        if (user == null) return (false, "Not found");
        if (user.Role == newRole) return (true, null);

        user.Role = newRole;
        await _userRepository.UpdateAsync(user);
        await _sessions.RevokeAllForUserAsync(targetUserId, "role_change");
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string username, string password, string email)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "Username is required");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return (false, "Password must be at least 8 characters");

        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email is required");

        var exists = await _userRepository.UserExistsAsync(username);
        if (exists)
            return (false, "Username already exists");

        var user = new User
        {
            Username = username.Trim(),
            PasswordHash = _jwtService.HashPassword(password),
            Email = email.Trim(),
            Role = "Member",
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);
        return (true, null);
    }

    private async Task<bool> IsLockedAsync(string key)
    {
        var row = await _throttles.GetOrCreateAsync(key);
        if (row.LockedUntil is DateTime until && until > DateTime.UtcNow)
            return true;
        if (row.LockedUntil != null && row.LockedUntil <= DateTime.UtcNow)
        {
            row.LockedUntil = null;
            row.FailureCount = 0;
            await _throttles.UpdateAsync(row);
        }
        return false;
    }

    private async Task RegisterFailureAsync(string key, string username, string? ip, string? userAgent)
    {
        var row = await _throttles.GetOrCreateAsync(key);
        row.FailureCount++;
        if (row.FailureCount >= _options.LockoutAfterFailures)
        {
            row.LockedUntil = DateTime.UtcNow.AddMinutes(_options.LockoutMinutes);
            await _throttles.UpdateAsync(row);
            await _audit.LogAsync(
                "ACCOUNT_LOCKED",
                null,
                username,
                "DENIED",
                "LoginThrottle",
                key,
                sourceIp: ip,
                userAgent: userAgent,
                changes: new { key, row.FailureCount, row.LockedUntil });
        }
        else
        {
            await _throttles.UpdateAsync(row);
        }
    }

    private async Task ResetThrottleAsync(string key)
    {
        var row = await _throttles.GetOrCreateAsync(key);
        row.FailureCount = 0;
        row.LockedUntil = null;
        await _throttles.UpdateAsync(row);
    }

    private async Task ApplyProgressiveDelayAsync(string userKey)
    {
        var row = await _throttles.GetOrCreateAsync(userKey);
        var steps = Math.Max(0, row.FailureCount - 1);
        var delay = (int)Math.Min(_options.DelayMaxMs, _options.DelayBaseMs * Math.Pow(2, steps));
        if (delay > 0)
            await Task.Delay(delay);
    }
}

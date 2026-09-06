using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuthSessionService _sessions;
    private readonly IAuditService _auditService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IAuthSessionService sessions,
        IAuditService auditService,
        IUserRepository userRepository,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _sessions = sessions;
        _auditService = auditService;
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var (success, access, refresh, expires, role, error) =
            await _authService.LoginAsync(request.Username, request.Password, ip, ua);

        if (!success)
        {
            await TryAuditLoginAsync("LOGIN_FAILED", null, request.Username, "DENIED",
                new { reasonCategory = "invalid_credentials" });
            return Unauthorized(ApiError.Build(TaskErrorCodes.Unauthorized, error ?? "Invalid username or password"));
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username);
        await TryAuditLoginAsync("LOGIN_SUCCEEDED", user?.EmployeeId, $"{request.Username} ({role ?? "User"})", "SUCCESS", null);

        return Ok(new LoginResponseDto
        {
            Token = access!,
            AccessToken = access!,
            RefreshToken = refresh!,
            Username = request.Username,
            Role = role ?? "User",
            ExpiresAt = expires ?? DateTime.UtcNow.AddMinutes(15)
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.RefreshToken))
            return Unauthorized(ApiError.Build(TaskErrorCodes.Unauthorized, "Invalid username or password"));

        var rotated = await _sessions.RotateAsync(
            body.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        if (rotated == null)
            return Unauthorized(ApiError.Build(TaskErrorCodes.Unauthorized, "Invalid username or password"));

        return Ok(new LoginResponseDto
        {
            Token = rotated.Value.AccessToken,
            AccessToken = rotated.Value.AccessToken,
            RefreshToken = rotated.Value.RefreshToken,
            ExpiresAt = rotated.Value.AccessExpiresAt
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] LoginRequestDto request)
    {
        var (success, error) = await _authService.RegisterAsync(
            request.Username,
            request.Password,
            $"{request.Username}@staffdesk.com");

        if (!success)
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, error ?? "Registration failed"));

        return Ok(new { message = "User registered successfully" });
    }

    [HttpPost("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto body)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (success, error) = await _authService.ChangePasswordAsync(userId.Value, body.CurrentPassword, body.NewPassword);
        if (!success)
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, error ?? "Could not change password"));

        var user = await _userRepository.GetByIdAsync(userId.Value);
        await TryAuditLoginAsync("PASSWORD_CHANGED", user?.EmployeeId, user?.Username ?? "user", "SUCCESS", null);
        return Ok(new { message = "Password changed. Sign in again." });
    }

    [HttpPut("users/{id:int}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleDto body)
    {
        var actor = GetUserId();
        if (actor == null) return Unauthorized();

        var (success, error) = await _authService.ChangeRoleAsync(id, body.Role, actor.Value);
        if (!success)
            return error == "Not found"
                ? NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Not found"))
                : BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, error ?? "Could not change role"));

        var target = await _userRepository.GetByIdAsync(id);
        await TryAuditLoginAsync("ROLE_CHANGED", target?.EmployeeId, target?.Username ?? "user", "SUCCESS",
            new { body.Role });
        return Ok(new { id, role = body.Role });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var sid = GetSessionId();
        if (sid != null)
            await _sessions.RevokeCurrentAsync(sid.Value, "logout");

        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? employeeId = null;
        var label = User.Identity?.Name ?? "unknown";
        if (int.TryParse(idClaim, out var userId))
        {
            var user = await _userRepository.GetByIdAsync(userId);
            employeeId = user?.EmployeeId;
            label = user != null ? $"{user.Username} ({user.Role})" : label;
        }

        await TryAuditLoginAsync("LOGOUT", employeeId, label, "SUCCESS", null);
        return Ok(new { message = "Logged out" });
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private Guid? GetSessionId()
    {
        var sid = User.FindFirst("sid")?.Value;
        return Guid.TryParse(sid, out var id) ? id : null;
    }

    private async Task TryAuditLoginAsync(string eventType, int? actorId, string actorLabel, string outcome, object? changes)
    {
        try
        {
            await _auditService.LogAsync(
                eventType: eventType,
                actorId: actorId,
                actorLabel: actorLabel,
                outcome: outcome,
                targetType: "Authentication",
                targetId: null,
                requestId: HttpContext.TraceIdentifier,
                sourceIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString(),
                changes: changes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write a login audit event ({EventType})", eventType);
        }
    }
}

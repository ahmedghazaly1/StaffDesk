using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StaffDesk.API.Common;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.API.Controllers;

// PL-11/PL-12: service-account API keys. Raw key shown once at creation; only the hash is stored.
// Creation, first use, and revocation are all audited (Appendix B: API_KEY_CREATED,
// API_KEY_USED_FIRST_TIME, API_KEY_REVOKED).
[ApiController]
[Route("v1/api-keys")]
[Authorize]
public class ApiKeysController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ApiKeysController(AppDbContext db, IAuditService audit, IUserRepository userRepository)
        : base(userRepository)
    {
        _db = db;
        _audit = audit;
    }

    // POST /v1/api-keys — create a new API key; returns the raw key once, never again.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest body)
    {
        var (employeeId, role, actorLabel, fail) = await ResolveActorAsync();
        if (fail != null) return fail;

        if (string.IsNullOrWhiteSpace(body.Label))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "Label is required."));

        var targetOwnerId = employeeId!.Value;
        if (body.OwnerId.HasValue && body.OwnerId.Value != targetOwnerId)
        {
            if (role != "Admin")
                return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden,
                    "Only Admin may create API keys for another employee."));
            targetOwnerId = body.OwnerId.Value;
        }

        var rawKey = GenerateRawKey();
        var hash = ComputeHash(rawKey);
        var prefix = rawKey[..8];
        var expiresAt = body.ExpiresInDays.HasValue
            ? DateTime.UtcNow.AddDays(body.ExpiresInDays.Value)
            : (DateTime?)null;

        var key = new ApiKey
        {
            Label = body.Label.Trim(),
            KeyHash = hash,
            KeyPrefix = prefix,
            OwnerId = targetOwnerId,
            Scopes = string.Join(",", body.Scopes ?? Array.Empty<string>()),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();

        await TryAuditAsync("API_KEY_CREATED", employeeId, actorLabel,
            "ApiKey", key.Id.ToString(), new { label = key.Label, scopes = key.Scopes, expiresAt });

        return Created($"/v1/api-keys/{key.Id}", new
        {
            key.Id,
            key.Label,
            key.KeyPrefix,
            key.Scopes,
            key.ExpiresAt,
            key.CreatedAt,
            // PL-12: raw key shown exactly once; not stored, not loggable.
            rawKey
        });
    }

    // GET /v1/api-keys — list caller's keys (Admin sees all; pass ?ownerId= to filter by owner).
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? ownerId = null)
    {
        var (employeeId, role, _, fail) = await ResolveActorAsync();
        if (fail != null) return fail;

        IQueryable<ApiKey> query = _db.ApiKeys.Where(k => !k.IsRevoked);

        if (role == "Admin" && ownerId.HasValue)
            query = query.Where(k => k.OwnerId == ownerId.Value);
        else if (role != "Admin")
            query = query.Where(k => k.OwnerId == employeeId!.Value);

        var keys = await query.OrderByDescending(k => k.CreatedAt).ToListAsync();
        return Ok(keys.Select(k => new
        {
            k.Id, k.Label, k.KeyPrefix, k.Scopes,
            k.IsRevoked, k.ExpiresAt, k.LastUsedAt, k.CreatedAt
        }));
    }

    // DELETE /v1/api-keys/{id} — revoke a key.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, [FromBody] RevokeApiKeyRequest? body)
    {
        var (employeeId, role, actorLabel, fail) = await ResolveActorAsync();
        if (fail != null) return fail;

        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null || (role != "Admin" && key.OwnerId != employeeId!.Value))
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "API key not found."));

        if (key.IsRevoked)
            return UnprocessableEntity(ApiError.Build(TaskErrorCodes.ValidationError, "Key is already revoked."));

        key.IsRevoked = true;
        key.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await TryAuditAsync("API_KEY_REVOKED", employeeId, actorLabel,
            "ApiKey", key.Id.ToString(), new { reason = body?.Reason });

        return NoContent();
    }

    // Internal: resolves a raw key for use by auth middleware (PL-12).
    public static async Task<ApiKey?> ResolveAndTouchAsync(AppDbContext db, string rawKey, IAuditService audit)
    {
        var hash = ComputeHash(rawKey);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && !k.IsRevoked);
        if (key == null) return null;
        if (key.ExpiresAt.HasValue && key.ExpiresAt < DateTime.UtcNow) return null;

        var isFirstUse = key.LastUsedAt == null;
        key.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (isFirstUse)
        {
            try
            {
                await audit.LogAsync("API_KEY_USED_FIRST_TIME", key.OwnerId, $"ApiKey:{key.KeyPrefix}",
                    "SUCCESS", "ApiKey", key.Id.ToString());
            }
            catch { }
        }

        return key;
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private async Task<(int? EmployeeId, string Role, string ActorLabel, IActionResult? Fail)> ResolveActorAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return (null, "", "", NoEmployeeLinkError());
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out var userId);
        var user = await UserRepository.GetByIdAsync(userId);
        var label = user != null ? $"{user.Username} ({user.Role})" : "unknown";
        return (employeeId, user?.Role ?? "", label, null);
    }

    private async Task TryAuditAsync(string eventType, int? actorId, string actorLabel,
        string targetType, string targetId, object? changes)
    {
        try
        {
            await _audit.LogAsync(eventType, actorId, actorLabel, "SUCCESS",
                targetType, targetId, changes: changes,
                requestId: HttpContext.TraceIdentifier,
                sourceIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());
        }
        catch { }
    }

    private static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "sdk_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static string ComputeHash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public record CreateApiKeyRequest(
    string Label,
    int? OwnerId,
    string[]? Scopes,
    int? ExpiresInDays);

public record RevokeApiKeyRequest(string? Reason);

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StaffDesk.API.Common;
using StaffDesk.Core;
using StaffDesk.Core.Constants;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.API.Controllers;

// PL-13/PL-14: outbound webhook subscriptions. Events are delivered via the job runner with
// exponential backoff; each delivery attempt is recorded and individually replayable by Admin.
[ApiController]
[Route("v1/webhooks")]
[Authorize]
public class WebhooksController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IJobService _jobs;

    public WebhooksController(AppDbContext db, IAuditService audit, IJobService jobs, IUserRepository userRepository)
        : base(userRepository)
    {
        _db = db;
        _audit = audit;
        _jobs = jobs;
    }

    // POST /v1/webhooks — create a subscription; signing secret shown once.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebhookRequest body)
    {
        var (employeeId, _, actorLabel, fail) = await ResolveActorAsync();
        if (fail != null) return fail;

        if (string.IsNullOrWhiteSpace(body.TargetUrl))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "TargetUrl is required."));

        if (!UrlAllowList.IsAllowed(body.TargetUrl))
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError,
                "TargetUrl must be https (or http on localhost)."));

        if (body.EventTypes == null || body.EventTypes.Length == 0)
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError,
                "At least one event type is required."));

        var rawSecret = GenerateSecret();
        var storedSecret = ComputeHash(rawSecret);

        var sub = new WebhookSubscription
        {
            Label = body.Label?.Trim() ?? body.TargetUrl,
            TargetUrl = body.TargetUrl,
            EventTypesJson = JsonSerializer.Serialize(body.EventTypes),
            SigningSecret = storedSecret,
            OwnerId = employeeId!.Value,
            DisableAfterConsecutiveFailures = body.DisableAfterFailures ?? 10,
            CreatedAt = DateTime.UtcNow
        };

        _db.WebhookSubscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await TryAuditAsync("WEBHOOK_CREATED", employeeId, actorLabel,
            "WebhookSubscription", sub.Id.ToString(),
            new { label = sub.Label, eventTypes = body.EventTypes });

        return Created($"/v1/webhooks/{sub.Id}", new
        {
            sub.Id, sub.Label, sub.TargetUrl, sub.EventTypesJson, sub.IsActive, sub.CreatedAt,
            // PL-13: signing secret shown once so the caller can configure verification on their end.
            signingSecret = rawSecret
        });
    }

    // GET /v1/webhooks — list subscriptions (Admin sees all).
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var (employeeId, role, _, fail) = await ResolveActorAsync();
        if (fail != null) return fail;

        IQueryable<WebhookSubscription> q = _db.WebhookSubscriptions;
        if (role != "Admin")
            q = q.Where(s => s.OwnerId == employeeId!.Value);

        var subs = await q.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return Ok(subs.Select(s => new
        {
            s.Id, s.Label, s.TargetUrl, s.EventTypesJson, s.IsActive,
            s.ConsecutiveFailures, s.DisableAfterConsecutiveFailures, s.CreatedAt
        }));
    }

    // DELETE /v1/webhooks/{id} — deactivate.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (employeeId, role, actorLabel, fail) = await ResolveActorAsync();
        if (fail != null) return fail;

        var sub = await _db.WebhookSubscriptions.FindAsync(id);
        if (sub == null || (role != "Admin" && sub.OwnerId != employeeId!.Value))
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Webhook subscription not found."));

        sub.IsActive = false;
        await _db.SaveChangesAsync();

        await TryAuditAsync("WEBHOOK_DISABLED", employeeId, actorLabel,
            "WebhookSubscription", sub.Id.ToString(), null);

        return NoContent();
    }

    // GET /v1/webhooks/{id}/deliveries — delivery history for a subscription.
    [HttpGet("{id:int}/deliveries")]
    public async Task<IActionResult> GetDeliveries(int id,
        [FromQuery] string? state = null, [FromQuery] int limit = 50)
    {
        var (employeeId, role, _, fail) = await ResolveActorAsync();
        if (fail != null) return fail;

        var sub = await _db.WebhookSubscriptions.FindAsync(id);
        if (sub == null || (role != "Admin" && sub.OwnerId != employeeId!.Value))
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Webhook subscription not found."));

        if (limit > 200) limit = 200;

        IQueryable<WebhookDelivery> q = _db.WebhookDeliveries.Where(d => d.SubscriptionId == id);
        if (!string.IsNullOrWhiteSpace(state))
            q = q.Where(d => d.State == state.ToUpperInvariant());

        var deliveries = await q.OrderByDescending(d => d.CreatedAt).Take(limit).ToListAsync();
        return Ok(deliveries.Select(d => new
        {
            d.Id, d.EventType, d.State, d.ResponseStatusCode,
            d.ErrorMessage, d.CreatedAt, d.DeliveredAt
        }));
    }

    // POST /v1/webhooks/deliveries/{id}/replay — Admin: re-queue a single delivery.
    [HttpPost("deliveries/{id:long}/replay")]
    public async Task<IActionResult> Replay(long id)
    {
        var (_, role, _, fail) = await ResolveActorAsync();
        if (fail != null) return fail;
        if (role != "Admin")
            return StatusCode(403, ApiError.Build(TaskErrorCodes.Forbidden, "Admin only."));

        var delivery = await _db.WebhookDeliveries
            .Include(d => d.Subscription)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (delivery == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Delivery not found."));

        var job = await _jobs.EnqueueAsync(JobTypes.WebhookDispatch, new
        {
            subscriptionId = delivery.SubscriptionId,
            eventType = delivery.EventType,
            payload = delivery.PayloadJson,
            replayOfDeliveryId = delivery.Id
        });

        return Accepted(new { jobId = job.Id, replayOfDeliveryId = delivery.Id });
    }

    // Dispatch runs in the worker via IWebhookDispatchService / WEBHOOK_DISPATCH jobs.

    private async Task<(int? EmployeeId, string Role, string ActorLabel, IActionResult? Fail)> ResolveActorAsync()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null) return (null, "", "", NoEmployeeLinkError());
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out var userId);
        var user = await UserRepository.GetByIdAsync(userId);
        return (employeeId, user?.Role ?? "", user != null ? $"{user.Username} ({user.Role})" : "unknown", null);
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

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "whsec_" + Convert.ToBase64String(bytes).TrimEnd('=');
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public record CreateWebhookRequest(
    string? Label,
    string TargetUrl,
    string[] EventTypes,
    int? DisableAfterFailures);

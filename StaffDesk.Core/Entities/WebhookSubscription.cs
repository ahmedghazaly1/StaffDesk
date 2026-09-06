namespace StaffDesk.Core.Entities;

// PL-13/PL-14: outbound webhook subscriptions. Each subscription receives events for the
// chosen event types, signed with an HMAC-SHA256 over the raw body plus a timestamp header.
public class WebhookSubscription
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>JSON array of event type strings to deliver, e.g. ["TASK_CREATED","TASK_DONE"].</summary>
    public string EventTypesJson { get; set; } = "[]";

    /// <summary>HMAC-SHA256 signing secret, stored hashed (raw shown once at creation).</summary>
    public string SigningSecret { get; set; } = string.Empty;

    public int OwnerId { get; set; }
    public Employee? Owner { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// PL-13: auto-disabled after this many consecutive failures. Reset to 0 on any success.
    /// </summary>
    public int ConsecutiveFailures { get; set; } = 0;

    /// <summary>Threshold at which the subscription is automatically disabled (configurable; default 10).</summary>
    public int DisableAfterConsecutiveFailures { get; set; } = 10;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// PL-14: every delivery attempt is recorded so individual ones can be replayed.
public class WebhookDelivery
{
    public long Id { get; set; }
    public int SubscriptionId { get; set; }
    public WebhookSubscription? Subscription { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Job that executed (or is executing) this delivery.</summary>
    public long? JobId { get; set; }

    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }

    public string State { get; set; } = "PENDING"; // PENDING, DELIVERED, FAILED

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
}

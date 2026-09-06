namespace StaffDesk.Core.Interfaces;

public interface IWebhookDispatchService
{
    Task DispatchAsync(int subscriptionId, string eventType, string payloadJson, long? replayOfDeliveryId = null);
}

public sealed class WebhookDispatchPayload
{
    public int SubscriptionId { get; set; }
    public string EventType { get; set; } = "";
    public string? Payload { get; set; }
    public string? PayloadJson { get; set; }
    public long? ReplayOfDeliveryId { get; set; }

    public string Body => Payload ?? PayloadJson ?? "{}";
}

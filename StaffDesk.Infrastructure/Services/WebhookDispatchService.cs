using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Services;

public sealed class WebhookDispatchService : IWebhookDispatchService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookDispatchService(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public static string Sign(string signingSecret, string timestamp, string body)
    {
        var sigPayload = $"{timestamp}.{body}";
        var hmacKey = Encoding.UTF8.GetBytes(signingSecret);
        using var hmac = new HMACSHA256(hmacKey);
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(sigPayload))).ToLowerInvariant();
    }

    public static bool Verify(string signingSecret, string timestamp, string body, string signatureHeader)
    {
        var expected = Sign(signingSecret, timestamp, body);
        return string.Equals(expected, signatureHeader, StringComparison.OrdinalIgnoreCase);
    }

    public async Task DispatchAsync(int subscriptionId, string eventType, string payloadJson, long? replayOfDeliveryId = null)
    {
        var sub = await _db.WebhookSubscriptions.FindAsync(subscriptionId);
        if (sub == null || !sub.IsActive) return;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig = Sign(sub.SigningSecret, timestamp, payloadJson);

        WebhookDelivery delivery;
        if (replayOfDeliveryId.HasValue)
        {
            delivery = await _db.WebhookDeliveries.FindAsync(replayOfDeliveryId.Value)
                       ?? CreateDelivery(subscriptionId, eventType, payloadJson);
            if (delivery.Id == 0) _db.WebhookDeliveries.Add(delivery);
        }
        else
        {
            delivery = CreateDelivery(subscriptionId, eventType, payloadJson);
            _db.WebhookDeliveries.Add(delivery);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookClient");
            using var req = new HttpRequestMessage(HttpMethod.Post, sub.TargetUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-StaffDesk-Event", eventType);
            req.Headers.TryAddWithoutValidation("X-StaffDesk-Timestamp", timestamp);
            req.Headers.TryAddWithoutValidation("X-StaffDesk-Signature", sig);

            var response = await client.SendAsync(req);
            delivery.ResponseStatusCode = (int)response.StatusCode;
            delivery.ResponseBody = await response.Content.ReadAsStringAsync();
            delivery.DeliveredAt = DateTime.UtcNow;

            if (response.IsSuccessStatusCode)
            {
                delivery.State = "DELIVERED";
                sub.ConsecutiveFailures = 0;
            }
            else
            {
                delivery.State = "FAILED";
                delivery.ErrorMessage = $"HTTP {(int)response.StatusCode}";
                sub.ConsecutiveFailures++;
            }
        }
        catch (Exception ex)
        {
            delivery.State = "FAILED";
            delivery.ErrorMessage = ex.Message;
            sub.ConsecutiveFailures++;
        }

        if (sub.ConsecutiveFailures >= sub.DisableAfterConsecutiveFailures)
            sub.IsActive = false;

        await _db.SaveChangesAsync();
    }

    private static WebhookDelivery CreateDelivery(int subId, string eventType, string payload) => new()
    {
        SubscriptionId = subId,
        EventType = eventType,
        PayloadJson = payload,
        State = "PENDING",
        CreatedAt = DateTime.UtcNow
    };
}

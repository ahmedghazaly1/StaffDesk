namespace StaffDesk.Core.Entities;

// Part F - PL-1..PL-3: lets a retried POST recognise itself instead of creating a duplicate.
// Scoped per caller (CallerId) so two different users can safely reuse the same key string.
public class IdempotencyRecord
{
    public int Id { get; set; }
    public int CallerId { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>SHA-256 of method+path+body, so a key reused with a different request is detected (PL-2).</summary>
    public string RequestHash { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; }
    public string ResponseBodyJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>PL-3: expires on a documented schedule and is swept up by the retention/purge job.</summary>
    public DateTime ExpiresAt { get; set; }
}

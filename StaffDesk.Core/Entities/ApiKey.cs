namespace StaffDesk.Core.Entities;

// PL-11/PL-12: non-human principals authenticating with a scoped, revocable API key.
// The raw key is shown exactly once at creation and never stored — only the SHA-256 hash is kept.
public class ApiKey
{
    public int Id { get; set; }

    /// <summary>Human-readable label so an admin can identify the key without seeing its value.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the raw key. Never store the raw value.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Prefix of the raw key (first 8 chars) shown in listings so users can identify which key this is.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>Employee this key belongs to. A service account may have no linked employee; use OwnerId instead.</summary>
    public int OwnerId { get; set; }
    public Employee? Owner { get; set; }

    /// <summary>Comma-separated permission scopes, e.g. "tasks:read,employees:read".</summary>
    public string Scopes { get; set; } = string.Empty;

    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }

    /// <summary>Optional hard expiry. Null = does not expire.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>PL-12: recorded on every authenticated request so stale keys are visible.</summary>
    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

namespace StaffDesk.Core.Entities;

/// <summary>SP-1/SP-2: one refresh-token session in a reusable family.</summary>
public class AuthSession
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string RefreshTokenHash { get; set; } = string.Empty;
    /// <summary>Previous hash after rotation — presenting it is reuse (SP-1).</summary>
    public string? PreviousRefreshTokenHash { get; set; }
    public DateTime RefreshExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public bool Replaced { get; set; }

    public string? UserAgent { get; set; }
    public string? Ip { get; set; }
}

/// <summary>SP-5: failed-login counters and lockout per account key and per IP key.</summary>
public class LoginThrottle
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int FailureCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

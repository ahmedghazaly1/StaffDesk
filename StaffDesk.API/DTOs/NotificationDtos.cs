namespace StaffDesk.API.DTOs;

// ============================================
// Notification Response
// ============================================
public class NotificationResponseDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? TaskId { get; set; }
    public string? TaskKey { get; set; }
    public string? TaskTitle { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// Notification List Response
// ============================================
public class NotificationListResponseDto
{
    public IEnumerable<NotificationResponseDto> Data { get; set; } = new List<NotificationResponseDto>();
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

namespace StaffDesk.API.DTOs;

// ============================================
// Create Comment
// ============================================
public class CommentCreateDto
{
    public string Body { get; set; } = string.Empty;
}

// ============================================
// Update Comment
// ============================================
public class CommentUpdateDto
{
    public string Body { get; set; } = string.Empty;
}

// ============================================
// Comment Response
// ============================================
public class CommentResponseDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string Body { get; set; } = string.Empty;
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorLevel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
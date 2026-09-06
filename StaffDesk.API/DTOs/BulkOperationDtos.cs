namespace StaffDesk.API.DTOs;

// ============================================
// Bulk Status Update
// ============================================
public class BulkStatusUpdateDto
{
    public List<int> TaskIds { get; set; } = new List<int>();
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

// ============================================
// Bulk Assignee Update
// ============================================
public class BulkAssigneeUpdateDto
{
    public List<int> TaskIds { get; set; } = new List<int>();
    public int? AssigneeId { get; set; }
}

// ============================================
// Bulk Delete
// ============================================
public class BulkDeleteDto
{
    public List<int> TaskIds { get; set; } = new List<int>();
}

// ============================================
// Bulk Archive
// ============================================
public class BulkArchiveDto
{
    public List<int> TaskIds { get; set; } = new List<int>();
    public bool IsArchived { get; set; }
}

// ============================================
// Bulk Tags
// ============================================
public class BulkTagsDto
{
    public List<int> TaskIds { get; set; } = new List<int>();
    public List<string> Tags { get; set; } = new List<string>();
    public string Action { get; set; } = "ADD";  // ADD or REMOVE
}

// ============================================
// Bulk Operation Result
// ============================================
public class BulkOperationResultDto
{
    public List<int> Successful { get; set; } = new List<int>();
    public List<BulkOperationFailure> Failed { get; set; } = new List<BulkOperationFailure>();
    public int TotalProcessed => Successful.Count + Failed.Count;
    public int TotalSuccessful => Successful.Count;
    public int TotalFailed => Failed.Count;
}

public class BulkOperationFailure
{
    public int TaskId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
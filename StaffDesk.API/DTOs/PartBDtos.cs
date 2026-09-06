namespace StaffDesk.API.DTOs;

public class SavedViewCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Visibility { get; set; } = "PRIVATE";
    public int? DepartmentId { get; set; }
    public string FilterJson { get; set; } = "{}";
}

public class SavedViewUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Visibility { get; set; } = "PRIVATE";
    public int? DepartmentId { get; set; }
    public string FilterJson { get; set; } = "{}";
}

public class SavedViewResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string FilterJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class HandoverRequestDto
{
    public int ToEmployeeId { get; set; }
}

public class ApprovalStepReassignDto
{
    public int NewApproverId { get; set; }
    public string? Reason { get; set; }
}

public class BulkJobResponseDto
{
    public long JobId { get; set; }
    public string State { get; set; } = "QUEUED";
    public string Message { get; set; } = string.Empty;
}

public class AddTriagerDto
{
    public int EmployeeId { get; set; }
}

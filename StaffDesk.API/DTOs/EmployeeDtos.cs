namespace StaffDesk.API.DTOs;

// For creating a new employee (POST request)
public class EmployeeCreateDto
{
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int LevelId { get; set; }  // NEW - Required
}

// For updating an employee (PUT request)
public class EmployeeUpdateDto
{
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int LevelId { get; set; }           // NEW
    public int? ManagerId { get; set; }        // NEW - Optional
    public bool IsActive { get; set; } = true; // NEW
}

// For employee responses (GET responses)
public class EmployeeResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }           // NEW
    public int LevelId { get; set; }                // NEW
    public string LevelName { get; set; } = string.Empty;  // NEW
    public int LevelRank { get; set; }              // NEW
    public int? ManagerId { get; set; }             // NEW
    public string? ManagerName { get; set; }        // NEW
    public bool IsActive { get; set; } = true;      // NEW
}

// For paginated responses
public class PaginatedResponseDto<T>
{
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / Limit);
}

// For employee update with manager
public class EmployeeUpdateManagerDto
{
    public int? ManagerId { get; set; }
}
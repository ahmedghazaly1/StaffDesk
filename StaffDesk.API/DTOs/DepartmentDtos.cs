namespace StaffDesk.API.DTOs;

// For creating a new department (POST request)
public class DepartmentCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

// For listing departments (GET response)
public class DepartmentListResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
}

// For single department with employee count (GET /departments/{id})
public class DepartmentDetailResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
}

// For updating department manager
public class DepartmentUpdateManagerDto
{
    public int? ManagerId { get; set; }
}
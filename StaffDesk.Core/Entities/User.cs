namespace StaffDesk.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    // ============================================
    // Role Constants
    // ============================================
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Member = "Member";
        public const string Auditor = "Auditor";
        public const string HrAdmin = "HR_ADMIN";
    }

    // Helper properties
    public bool IsAdmin => Role == Roles.Admin;
    public bool IsAuditor => Role == Roles.Auditor;
    public bool IsHrAdmin => Role == Roles.HrAdmin;
    public bool IsManager => Role == Roles.Manager;
}
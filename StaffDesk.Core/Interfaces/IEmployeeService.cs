using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IEmployeeService
{
    Task<IEnumerable<Employee>> GetAllAsync(int? page = null, int? limit = null,
                                            string? search = null, int? departmentId = null,
                                            int? levelId = null, int? managerId = null, bool? isActive = null);
    Task<Employee?> GetByIdAsync(int id);
    Task<Employee> CreateAsync(string fullName, string jobTitle, int departmentId, int levelId);
    Task<int> GetTotalCountAsync(string? search = null, int? departmentId = null,
                                 int? levelId = null, int? managerId = null, bool? isActive = null);
    Task<IEnumerable<Employee>> GetByDepartmentIdAsync(int departmentId);
    Task<Employee> UpdateAsync(int id, string fullName, string jobTitle, int departmentId, int levelId, int? managerId, bool isActive, int actorUserId);

    // Deactivates the employee (soft) rather than deleting the row, so task/activity history is preserved.
    Task<bool> DeactivateAsync(int id, int actorUserId);

    Task<Employee> UpdateManagerAsync(int id, int? managerId, int actorUserId);

    Task<IEnumerable<Employee>> GetDirectReportsAsync(int id);

    Task<IEnumerable<Employee>> GetReportingChainAsync(int id);

    Task<IEnumerable<EmployeeActivity>> GetActivityAsync(int id);
}

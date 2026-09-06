using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IEmployeeRepository
{
    Task<IEnumerable<Employee>> GetAllAsync(int? page = null, int? limit = null,
                                            string? search = null, int? departmentId = null,
                                            int? levelId = null, int? managerId = null, bool? isActive = null);
    Task<Employee?> GetByIdAsync(int id);
    Task<Employee> CreateAsync(Employee employee);
    Task<int> GetTotalCountAsync(string? search = null, int? departmentId = null,
                                 int? levelId = null, int? managerId = null, bool? isActive = null);
    Task<IEnumerable<Employee>> GetByDepartmentIdAsync(int departmentId);
    Task<Employee> UpdateAsync(Employee employee);

    Task<Employee> UpdateAsync(int id, string fullName, string jobTitle, int? departmentId = null);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<Employee>> GetDirectReportsAsync(int id);

    Task AddActivityAsync(EmployeeActivity activity);
    Task<IEnumerable<EmployeeActivity>> GetActivityAsync(int employeeId);
}

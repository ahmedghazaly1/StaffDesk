using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IDepartmentRepository
{
    Task<IEnumerable<Department>> GetAllAsync();
    Task<Department?> GetByIdAsync(int id);
    Task<Department> CreateAsync(Department department);
    Task<bool> ExistsAsync(int id);
    Task<Department> UpdateAsync(Department department);  
    Task<Department> UpdateAsync(int id, string name, string location);
    Task<bool> DeleteAsync(int id);

    Task<IEnumerable<DepartmentTriager>> GetTriagersAsync(int departmentId);
    Task<DepartmentTriager> AddTriagerAsync(int departmentId, int employeeId);
    Task<bool> RemoveTriagerAsync(int departmentId, int employeeId);
    Task<bool> IsTriagerAsync(int departmentId, int employeeId);

    /// <summary>WC-8: department ids this employee has triage authority over - as the department's
    /// manager, or as a designated triager. Used to auto-resolve an omitted departmentId on the
    /// triage queue rather than binding it to 0 and failing the authority check.</summary>
    Task<List<int>> GetManagedDepartmentIdsAsync(int employeeId);
}
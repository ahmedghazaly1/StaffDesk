using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IDepartmentService
{
    Task<IEnumerable<Department>> GetAllAsync();
    Task<Department?> GetByIdAsync(int id);
    Task<Department> CreateAsync(string name, string location);
    Task<Department> UpdateAsync(int id, string name, string location);
    Task<bool> DeleteAsync(int id);
    Task<Department> UpdateManagerAsync(int id, int? managerId);
}
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _repository;
    private readonly IEmployeeRepository _employeeRepository;  // ADD THIS

    public DepartmentService(IDepartmentRepository repository, IEmployeeRepository employeeRepository)  // ADD employeeRepository parameter
    {
        _repository = repository;
        _employeeRepository = employeeRepository;  // ADD THIS
    }

    public async Task<IEnumerable<Department>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Department?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Department> CreateAsync(string name, string location)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department name is required");
        
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required");

        var department = new Department 
        { 
            Name = name.Trim(), 
            Location = location.Trim() 
        };
        
        return await _repository.CreateAsync(department);
    }

    public async Task<Department> UpdateAsync(int id, string name, string location)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department name is required");
        
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required");

        var department = await _repository.GetByIdAsync(id);
        if (department == null)
            throw new ArgumentException("Department not found");

        department.Name = name.Trim();
        department.Location = location.Trim();
        
        return await _repository.UpdateAsync(department);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    // ============================================
    // Update department manager
    // ============================================
    public async Task<Department> UpdateManagerAsync(int id, int? managerId)
    {
        var department = await _repository.GetByIdAsync(id);
        if (department == null)
            throw new ArgumentException("Department not found");

        if (managerId.HasValue)
        {
            var manager = await _employeeRepository.GetByIdAsync(managerId.Value);
            if (manager == null)
                throw new ArgumentException("Manager not found");

            if (manager.DepartmentId != id)
                throw new ArgumentException("Manager must belong to the department they manage");
        }

        department.ManagerId = managerId;
        return await _repository.UpdateAsync(department);
    }
}
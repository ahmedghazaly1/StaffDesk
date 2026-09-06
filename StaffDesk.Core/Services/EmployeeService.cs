using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly ISeniorityLevelRepository _levelRepo;

    public EmployeeService(IEmployeeRepository employeeRepo, IDepartmentRepository departmentRepo, ISeniorityLevelRepository levelRepo)
    {
        _employeeRepo = employeeRepo;
        _departmentRepo = departmentRepo;
        _levelRepo = levelRepo;
    }

    public async Task<IEnumerable<Employee>> GetAllAsync(int? page = null, int? limit = null,
                                                         string? search = null, int? departmentId = null,
                                                         int? levelId = null, int? managerId = null, bool? isActive = null)
    {
        int pageValue = page.GetValueOrDefault(1);
        int limitValue = limit.GetValueOrDefault(10);

        if (pageValue < 1) pageValue = 1;
        if (limitValue < 1) limitValue = 10;
        if (limitValue > 100) limitValue = 100;

        return await _employeeRepo.GetAllAsync(pageValue, limitValue, search, departmentId, levelId, managerId, isActive);
    }

    public async Task<Employee?> GetByIdAsync(int id)
    {
        return await _employeeRepo.GetByIdAsync(id);
    }

    public async Task<Employee> CreateAsync(string fullName, string jobTitle, int departmentId, int levelId)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required");

        if (string.IsNullOrWhiteSpace(jobTitle))
            throw new ArgumentException("Job title is required");

        var departmentExists = await _departmentRepo.ExistsAsync(departmentId);
        if (!departmentExists)
            throw new ArgumentException("Department does not exist");

        var levelExists = await _levelRepo.ExistsAsync(levelId);
        if (!levelExists)
            throw new ArgumentException("Seniority level does not exist");

        var employee = new Employee
        {
            FullName = fullName.Trim(),
            JobTitle = jobTitle.Trim(),
            DepartmentId = departmentId,
            LevelId = levelId,
            IsActive = true
        };

        return await _employeeRepo.CreateAsync(employee);
    }

    public async Task<Employee> UpdateAsync(int id, string fullName, string jobTitle, int departmentId, int levelId, int? managerId, bool isActive, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required");

        if (string.IsNullOrWhiteSpace(jobTitle))
            throw new ArgumentException("Job title is required");

        var employee = await _employeeRepo.GetByIdAsync(id);
        if (employee == null)
            throw new ArgumentException("Employee not found");

        var departmentExists = await _departmentRepo.ExistsAsync(departmentId);
        if (!departmentExists)
            throw new ArgumentException("Department does not exist");

        var levelExists = await _levelRepo.ExistsAsync(levelId);
        if (!levelExists)
            throw new ArgumentException("Seniority level does not exist");

        if (managerId.HasValue)
        {
            if (managerId.Value == id)
                throw new ReportingCycleException("Employee cannot be their own manager");

            var managerExists = await _employeeRepo.GetByIdAsync(managerId.Value);
            if (managerExists == null)
                throw new ArgumentException("Manager does not exist");

            if (await WouldCreateCycleAsync(id, managerId.Value))
                throw new ReportingCycleException("This would create a reporting cycle");
        }

        var oldLevelId = employee.LevelId;
        var oldManagerId = employee.ManagerId;
        var oldDepartmentId = employee.DepartmentId;
        var oldIsActive = employee.IsActive;

        employee.FullName = fullName.Trim();
        employee.JobTitle = jobTitle.Trim();
        employee.DepartmentId = departmentId;
        employee.LevelId = levelId;
        employee.ManagerId = managerId;
        employee.IsActive = isActive;
        employee.UpdatedAt = DateTime.UtcNow;

        var updated = await _employeeRepo.UpdateAsync(employee);

        await LogChangeIfDifferentAsync(id, actorUserId, "LevelId", oldLevelId, levelId);
        await LogChangeIfDifferentAsync(id, actorUserId, "ManagerId", oldManagerId, managerId);
        await LogChangeIfDifferentAsync(id, actorUserId, "DepartmentId", oldDepartmentId, departmentId);
        await LogChangeIfDifferentAsync(id, actorUserId, "IsActive", oldIsActive, isActive);

        return updated;
    }

    // Employees are deactivated rather than deleted, so task history and reporting-chain
    // references stay intact (OR-8: deactivated employees remain readable/attached to history).
    public async Task<bool> DeactivateAsync(int id, int actorUserId)
    {
        var employee = await _employeeRepo.GetByIdAsync(id);
        if (employee == null) return false;

        if (!employee.IsActive) return true;

        employee.IsActive = false;
        await _employeeRepo.UpdateAsync(employee);

        await _employeeRepo.AddActivityAsync(new EmployeeActivity
        {
            EmployeeId = id,
            ActorUserId = actorUserId,
            Field = "IsActive",
            OldValue = "true",
            NewValue = "false"
        });

        return true;
    }

    public async Task<int> GetTotalCountAsync(string? search = null, int? departmentId = null,
                                              int? levelId = null, int? managerId = null, bool? isActive = null)
    {
        return await _employeeRepo.GetTotalCountAsync(search, departmentId, levelId, managerId, isActive);
    }

    public async Task<IEnumerable<Employee>> GetByDepartmentIdAsync(int departmentId)
    {
        return await _employeeRepo.GetByDepartmentIdAsync(departmentId);
    }

    public async Task<Employee> UpdateManagerAsync(int id, int? managerId, int actorUserId)
    {
        var employee = await _employeeRepo.GetByIdAsync(id);
        if (employee == null)
            throw new ArgumentException("Employee not found");

        var oldManagerId = employee.ManagerId;

        if (managerId.HasValue)
        {
            if (managerId.Value == id)
                throw new ReportingCycleException("Employee cannot be their own manager");

            var managerExists = await _employeeRepo.GetByIdAsync(managerId.Value);
            if (managerExists == null)
                throw new ArgumentException("Manager does not exist");

            if (await WouldCreateCycleAsync(id, managerId.Value))
                throw new ReportingCycleException("This would create a reporting cycle");
        }

        employee.ManagerId = managerId;
        var updated = await _employeeRepo.UpdateAsync(employee);

        await LogChangeIfDifferentAsync(id, actorUserId, "ManagerId", oldManagerId, managerId);

        return updated;
    }

    public async Task<IEnumerable<Employee>> GetDirectReportsAsync(int id)
    {
        var employee = await _employeeRepo.GetByIdAsync(id);
        if (employee == null)
            throw new ArgumentException("Employee not found");

        return await _employeeRepo.GetDirectReportsAsync(id);
    }

    public async Task<IEnumerable<Employee>> GetReportingChainAsync(int id)
    {
        var employee = await _employeeRepo.GetByIdAsync(id);
        if (employee == null)
            throw new ArgumentException("Employee not found");

        var chain = new List<Employee>();
        var current = employee;

        while (current != null && current.ManagerId.HasValue)
        {
            var manager = await _employeeRepo.GetByIdAsync(current.ManagerId.Value);
            if (manager == null) break;
            chain.Add(manager);
            current = manager;
        }

        return chain;
    }

    public async Task<IEnumerable<EmployeeActivity>> GetActivityAsync(int id)
    {
        return await _employeeRepo.GetActivityAsync(id);
    }

    private async Task LogChangeIfDifferentAsync(int employeeId, int actorUserId, string field, object? oldValue, object? newValue)
    {
        var oldStr = oldValue?.ToString();
        var newStr = newValue?.ToString();
        if (oldStr == newStr) return;

        await _employeeRepo.AddActivityAsync(new EmployeeActivity
        {
            EmployeeId = employeeId,
            ActorUserId = actorUserId,
            Field = field,
            OldValue = oldStr,
            NewValue = newStr
        });
    }

    private async Task<bool> WouldCreateCycleAsync(int employeeId, int newManagerId)
    {
        var visited = new HashSet<int>();
        int? currentId = newManagerId;

        while (currentId.HasValue && !visited.Contains(currentId.Value))
        {
            if (currentId.Value == employeeId)
                return true;

            visited.Add(currentId.Value);

            var current = await _employeeRepo.GetByIdAsync(currentId.Value);
            currentId = current?.ManagerId;
        }

        return false;
    }
}

// Distinguishes reporting-cycle violations (OR-6 -> 422) from ordinary validation errors (400).
public class ReportingCycleException : ArgumentException
{
    public ReportingCycleException(string message) : base(message) { }
}

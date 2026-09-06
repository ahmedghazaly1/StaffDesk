using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _context;

    public EmployeeRepository(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<Employee> ApplyFilters(IQueryable<Employee> query, string? search, int? departmentId,
                                              int? levelId, int? managerId, bool? isActive)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.FullName.ToLower().Contains(search.ToLower()));
        }

        if (departmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (levelId.HasValue)
        {
            query = query.Where(e => e.LevelId == levelId.Value);
        }

        if (managerId.HasValue)
        {
            query = query.Where(e => e.ManagerId == managerId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(e => e.IsActive == isActive.Value);
        }

        return query;
    }

    public async Task<IEnumerable<Employee>> GetAllAsync(int? page = null, int? limit = null,
                                                         string? search = null, int? departmentId = null,
                                                         int? levelId = null, int? managerId = null, bool? isActive = null)
    {
        IQueryable<Employee> query = _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Level)
            .Include(e => e.Manager)
            .AsQueryable();

        query = ApplyFilters(query, search, departmentId, levelId, managerId, isActive);

        if (page.HasValue && limit.HasValue)
        {
            var skip = (page.Value - 1) * limit.Value;
            query = query.Skip(skip).Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Employee?> GetByIdAsync(int id)
    {
        return await _context.Employees
            .Include(e => e.Department)
            .Include(e => e.Level)
            .Include(e => e.Manager)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Employee> CreateAsync(Employee employee)
    {
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
        return employee;
    }

    public async Task<int> GetTotalCountAsync(string? search = null, int? departmentId = null,
                                              int? levelId = null, int? managerId = null, bool? isActive = null)
    {
        IQueryable<Employee> query = _context.Employees.AsQueryable();
        query = ApplyFilters(query, search, departmentId, levelId, managerId, isActive);
        return await query.CountAsync();
    }

    public async Task<IEnumerable<Employee>> GetByDepartmentIdAsync(int departmentId)
    {
        return await _context.Employees
            .Include(e => e.Department)
            .Where(e => e.DepartmentId == departmentId)
            .ToListAsync();
    }

    public async Task<Employee> UpdateAsync(Employee employee)
    {
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();
        return employee;
    }

    public async Task<Employee> UpdateAsync(int id, string fullName, string jobTitle, int? departmentId = null)
    {
        var employee = await _context.Employees.FindAsync(id)
            ?? throw new ArgumentException("Employee not found");

        employee.FullName = fullName;
        employee.JobTitle = jobTitle;
        if (departmentId.HasValue)
            employee.DepartmentId = departmentId.Value;

        await _context.SaveChangesAsync();
        return employee;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return false;

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Employee>> GetDirectReportsAsync(int id)
    {
        return await _context.Employees
            .Include(e => e.Level)
            .Include(e => e.Department)
            .Where(e => e.ManagerId == id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();
    }

    public async Task AddActivityAsync(EmployeeActivity activity)
    {
        _context.EmployeeActivities.Add(activity);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<EmployeeActivity>> GetActivityAsync(int employeeId)
    {
        return await _context.EmployeeActivities
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}

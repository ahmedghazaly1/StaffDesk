using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _context;

    public DepartmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Department>> GetAllAsync()
    {
        return await _context.Departments
            .Include(d => d.Manager)
            .ToListAsync();
    }

    public async Task<Department?> GetByIdAsync(int id)
    {
        return await _context.Departments
            .Include(d => d.Employees)
            .Include(d => d.Manager)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Department> CreateAsync(Department department)
    {
        _context.Departments.Add(department);
        await _context.SaveChangesAsync();
        return department;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Departments.AnyAsync(d => d.Id == id);
    }

    public async Task<Department> UpdateAsync(Department department)
{
    _context.Departments.Update(department);
    await _context.SaveChangesAsync();
    return department;
}

public async Task<Department> UpdateAsync(int id, string name, string location)
{
    var department = await _context.Departments
        .Include(d => d.Manager)
        .FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new ArgumentException("Department not found");

    department.Name = name;
    department.Location = location;

    await _context.SaveChangesAsync();
    return department;
}

public async Task<bool> DeleteAsync(int id)
{
    var department = await _context.Departments.FindAsync(id);
    if (department == null) return false;

    // Every RESTRICT foreign key onto Departments must be checked here before attempting the
    // delete. Only "Employees" was checked previously; a department with rows in any of the
    // other RESTRICT-referencing tables (DailyMetricSnapshots, SlaPolicies, TaskRequests,
    // TaskTemplates, Tasks) hit the DB's own FK violation instead - which, because deletion
    // happens inside the same DbContext that later flushes the audit-write's SaveChangesAsync,
    // surfaced as a misleading "AUDIT_WRITE_FAILED" 500 rather than a clean error naming the
    // real blocker. (CASCADE/SET NULL foreign keys - DepartmentDefaultCriteria,
    // DepartmentTriagers, SavedViews - don't need a check; the DB handles those on its own.)
    if (await _context.Employees.AnyAsync(e => e.DepartmentId == id))
        throw new InvalidOperationException("Cannot delete a department that still has employees");
    if (await _context.Tasks.AnyAsync(t => t.DepartmentId == id))
        throw new InvalidOperationException("Cannot delete a department that still has tasks");
    if (await _context.TaskRequests.AnyAsync(r => r.DepartmentId == id))
        throw new InvalidOperationException("Cannot delete a department that still has task requests");
    if (await _context.SlaPolicies.AnyAsync(p => p.DepartmentId == id))
        throw new InvalidOperationException("Cannot delete a department that still has SLA policies");
    if (await _context.TaskTemplates.AnyAsync(t => t.DepartmentId == id))
        throw new InvalidOperationException("Cannot delete a department that still has task templates");
    if (await _context.DailyMetricSnapshots.AnyAsync(s => s.DepartmentId == id))
        throw new InvalidOperationException("Cannot delete a department that still has analytics history");

    _context.Departments.Remove(department);
    await _context.SaveChangesAsync();
    return true;
}

    public async Task<IEnumerable<DepartmentTriager>> GetTriagersAsync(int departmentId)
    {
        return await _context.DepartmentTriagers
            .Include(t => t.Employee)
            .Where(t => t.DepartmentId == departmentId)
            .OrderBy(t => t.Employee.FullName)
            .ToListAsync();
    }

    public async Task<DepartmentTriager> AddTriagerAsync(int departmentId, int employeeId)
    {
        var exists = await _context.DepartmentTriagers
            .AnyAsync(t => t.DepartmentId == departmentId && t.EmployeeId == employeeId);
        if (exists)
            throw new InvalidOperationException("Employee is already a triager for this department");

        var triager = new DepartmentTriager
        {
            DepartmentId = departmentId,
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow
        };
        _context.DepartmentTriagers.Add(triager);
        await _context.SaveChangesAsync();
        return triager;
    }

    public async Task<bool> RemoveTriagerAsync(int departmentId, int employeeId)
    {
        var triager = await _context.DepartmentTriagers
            .FirstOrDefaultAsync(t => t.DepartmentId == departmentId && t.EmployeeId == employeeId);
        if (triager == null) return false;
        _context.DepartmentTriagers.Remove(triager);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsTriagerAsync(int departmentId, int employeeId)
    {
        return await _context.DepartmentTriagers
            .AnyAsync(t => t.DepartmentId == departmentId && t.EmployeeId == employeeId);
    }

    public async Task<List<int>> GetManagedDepartmentIdsAsync(int employeeId)
    {
        var asManager = await _context.Departments
            .Where(d => d.ManagerId == employeeId)
            .Select(d => d.Id)
            .ToListAsync();

        var asTriager = await _context.DepartmentTriagers
            .Where(t => t.EmployeeId == employeeId)
            .Select(t => t.DepartmentId)
            .ToListAsync();

        return asManager.Union(asTriager).Distinct().ToList();
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Services;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Repositories;
using Xunit;

namespace StaffDesk.Tests;

/// <summary>
/// Regression tests for DELETE /v1/departments/{id} and DELETE /v1/seniority-levels/{id}
/// surfacing an unhandled 500 (misleadingly labeled AUDIT_WRITE_FAILED) instead of a clean 409
/// when a RESTRICT foreign key other than the one already-checked table blocks the delete.
/// Uses the InMemory provider directly, since these are plain repository/service dependencies
/// rather than the fuller Phase3World harness.
/// </summary>
public class DepartmentDeletionTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Delete_Succeeds_For_An_Empty_Department()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Empty Dept", Location = "HQ" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var service = new DepartmentService(new DepartmentRepository(db), new EmployeeRepository(db));
        var deleted = await service.DeleteAsync(dept.Id);

        Assert.True(deleted);
        Assert.Null(await db.Departments.FindAsync(dept.Id));
    }

    [Fact]
    public async Task Delete_Blocked_By_Employees_Throws_Clean_Exception()
    {
        await using var db = NewDb();
        var level = new SeniorityLevel { Name = "Mid", Rank = 30, IsActive = true };
        db.SeniorityLevels.Add(level);
        var dept = new Department { Name = "Staffed Dept", Location = "HQ" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        db.Employees.Add(new Employee { FullName = "X", JobTitle = "Y", DepartmentId = dept.Id, LevelId = level.Id, IsActive = true });
        await db.SaveChangesAsync();

        var service = new DepartmentService(new DepartmentRepository(db), new EmployeeRepository(db));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(dept.Id));
        Assert.Contains("employees", ex.Message, StringComparison.OrdinalIgnoreCase);

        // The department must still exist - a rejected delete is not a partial delete.
        Assert.NotNull(await db.Departments.FindAsync(dept.Id));
    }

    [Fact]
    public async Task Delete_Blocked_By_Tasks_Throws_Clean_Exception_Instead_Of_FK_Violation()
    {
        // This is the exact bug: Tasks.DepartmentId -> Departments is RESTRICT, but only
        // Employees was checked before the fix - a department with tasks and no employees hit
        // the DB's own FK violation, which (via the shared-DbContext SaveChangesAsync-batching
        // pattern) surfaced as an unhandled 500 during the audit write instead of a clean error.
        await using var db = NewDb();
        var level = new SeniorityLevel { Name = "Mid", Rank = 30, IsActive = true };
        db.SeniorityLevels.Add(level);
        var dept = new Department { Name = "Dept With Tasks", Location = "HQ" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        db.Tasks.Add(new WorkTask
        {
            Key = "TSK-1", Title = "A task", DepartmentId = dept.Id, CreatedById = 1,
            Status = "OPEN", Priority = "NORMAL"
        });
        await db.SaveChangesAsync();

        var service = new DepartmentService(new DepartmentRepository(db), new EmployeeRepository(db));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(dept.Id));
        Assert.Contains("tasks", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await db.Departments.FindAsync(dept.Id));
    }

    [Fact]
    public async Task Delete_Blocked_By_Sla_Policies_Throws_Clean_Exception()
    {
        await using var db = NewDb();
        var dept = new Department { Name = "Dept With Sla", Location = "HQ" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        db.SlaPolicies.Add(new SlaPolicy
        {
            DepartmentId = dept.Id, Priority = "HIGH",
            ResponseTargetMinutes = 60, ResolutionTargetMinutes = 480
        });
        await db.SaveChangesAsync();

        var service = new DepartmentService(new DepartmentRepository(db), new EmployeeRepository(db));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(dept.Id));
        Assert.Contains("SLA", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_Blocked_By_Daily_Metric_Snapshots_Throws_Clean_Exception()
    {
        // The exact table from the reported live repro.
        await using var db = NewDb();
        var dept = new Department { Name = "Dept With Analytics", Location = "HQ" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        db.DailyMetricSnapshots.Add(new DailyMetricSnapshot
        {
            DepartmentId = dept.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow), Version = 1
        });
        await db.SaveChangesAsync();

        var service = new DepartmentService(new DepartmentRepository(db), new EmployeeRepository(db));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(dept.Id));
        Assert.Contains("analytics", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Same bug class, second confirmed instance: SeniorityLevel delete only checked Employees,
    // not CompetencyLevelDescriptors (also RESTRICT) - every seeded level has descriptors, so
    // this reproduced on every single seniority level in the live database.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SeniorityLevel_Delete_Blocked_By_Competency_Descriptors_Throws_Clean_Exception()
    {
        await using var db = NewDb();
        var level = new SeniorityLevel { Name = "Mid", Rank = 30, IsActive = true };
        db.SeniorityLevels.Add(level);
        var competency = new Competency { Name = "Quality of work", Description = "d", Category = "Delivery", IsActive = true };
        db.Competencies.Add(competency);
        await db.SaveChangesAsync();
        db.CompetencyLevelDescriptors.Add(new CompetencyLevelDescriptor
        {
            CompetencyId = competency.Id, SeniorityLevelId = level.Id, Descriptor = "Meets expectations"
        });
        await db.SaveChangesAsync();

        var service = new SeniorityLevelService(new SeniorityLevelRepository(db));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(level.Id));
        Assert.Contains("competency", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await db.SeniorityLevels.FindAsync(level.Id));
    }

    [Fact]
    public async Task SeniorityLevel_Delete_Succeeds_When_Unused()
    {
        await using var db = NewDb();
        var level = new SeniorityLevel { Name = "Unused Level", Rank = 999, IsActive = true };
        db.SeniorityLevels.Add(level);
        await db.SaveChangesAsync();

        var service = new SeniorityLevelService(new SeniorityLevelRepository(db));
        var deleted = await service.DeleteAsync(level.Id);

        Assert.True(deleted);
        Assert.Null(await db.SeniorityLevels.FindAsync(level.Id));
    }
}

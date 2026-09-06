using System.Reflection;
using StaffDesk.API.Controllers;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using Xunit;

namespace StaffDesk.Tests;

/// <summary>
/// Regression tests for three permission-enforcement bugs found while manually testing the API
/// with different roles: POST /v1/departments had no role check, a Member could create a task in
/// a department they don't belong to, and a Member could set URGENT priority at task-creation
/// time even though the same rule is enforced on the dedicated priority-change path.
/// </summary>
public class PermissionEnforcementTests
{
    // ------------------------------------------------------------------
    // Bug 1: POST /v1/departments had no [AdminOnly], unlike PUT/DELETE.
    // ------------------------------------------------------------------
    [Fact]
    public void DepartmentsController_Create_Is_AdminOnly()
    {
        var method = typeof(DepartmentsController).GetMethod(nameof(DepartmentsController.Create));
        Assert.NotNull(method);
        Assert.NotEmpty(method!.GetCustomAttributes<AdminOnlyAttribute>());
    }

    [Fact]
    public void DepartmentsController_Update_And_Delete_Are_Still_AdminOnly()
    {
        // Regression guard: confirms the working checks weren't disturbed while fixing Create.
        var update = typeof(DepartmentsController).GetMethod(nameof(DepartmentsController.Update));
        var delete = typeof(DepartmentsController).GetMethod(nameof(DepartmentsController.Delete));
        Assert.NotEmpty(update!.GetCustomAttributes<AdminOnlyAttribute>());
        Assert.NotEmpty(delete!.GetCustomAttributes<AdminOnlyAttribute>());
    }

    // ------------------------------------------------------------------
    // Bug 2: a Member could create a task in a department they don't belong to.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Member_Cannot_Create_Task_In_Another_Department()
    {
        var world = await Phase3World.CreateAsync();
        await using (world)
        {
            var svc = world.TaskService();

            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.CreateTaskAsync(
                    title: "Cross-department task",
                    description: null,
                    departmentName: world.OtherDept.Name,
                    createdById: world.Member.Id, // Member belongs to world.Dept, not OtherDept
                    priority: "NORMAL"));

            Assert.Equal(TaskErrorCodes.Forbidden, ex.Code);
            Assert.Equal(403, ex.HttpStatus);
        }
    }

    [Fact]
    public async Task Member_Can_Still_Create_Task_In_Own_Department()
    {
        // Regression guard: the new check must not block the ordinary, allowed case.
        var world = await Phase3World.CreateAsync();
        await using (world)
        {
            var svc = world.TaskService();

            var task = await svc.CreateTaskAsync(
                title: "Own-department task",
                description: null,
                departmentName: world.Dept.Name,
                createdById: world.Member.Id,
                priority: "NORMAL");

            Assert.Equal(world.Dept.Id, task.DepartmentId);
        }
    }

    [Fact]
    public async Task Admin_Can_Create_Task_In_Any_Department()
    {
        var world = await Phase3World.CreateAsync();
        await using (world)
        {
            var svc = world.TaskService();

            // world.Manager holds the Manager role in the harness, not Admin - promote a fresh
            // Admin-role user via the existing seeded employee to exercise the ADMIN-Y case.
            var adminUser = world.ManagerUser;
            adminUser.Role = StaffDesk.Core.Entities.User.Roles.Admin;
            world.Db.Users.Update(adminUser);
            await world.Db.SaveChangesAsync();

            var task = await svc.CreateTaskAsync(
                title: "Admin cross-department task",
                description: null,
                departmentName: world.OtherDept.Name,
                createdById: world.Manager.Id,
                priority: "NORMAL");

            Assert.Equal(world.OtherDept.Id, task.DepartmentId);
        }
    }

    // ------------------------------------------------------------------
    // Bug 3: a Member could set URGENT priority at task-creation time.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Member_Cannot_Set_Urgent_Priority_At_Creation()
    {
        var world = await Phase3World.CreateAsync();
        await using (world)
        {
            var svc = world.TaskService();

            var ex = await Assert.ThrowsAsync<TaskDomainException>(() =>
                svc.CreateTaskAsync(
                    title: "Urgent task",
                    description: null,
                    departmentName: world.Dept.Name,
                    createdById: world.Member.Id,
                    priority: "URGENT"));

            Assert.Equal(TaskErrorCodes.AssignmentNotPermitted, ex.Code);
            Assert.Equal(403, ex.HttpStatus);
        }
    }

    [Fact]
    public async Task DepartmentManager_Can_Set_Urgent_Priority_At_Creation()
    {
        // Section 8.2: MANAGER is "D" (permitted within their own department) for URGENT.
        var world = await Phase3World.CreateAsync();
        await using (world)
        {
            var svc = world.TaskService();

            var task = await svc.CreateTaskAsync(
                title: "Urgent task by dept manager",
                description: null,
                departmentName: world.Dept.Name,
                createdById: world.Manager.Id, // world.Dept.ManagerId == world.Manager.Id
                priority: "URGENT");

            Assert.Equal("URGENT", task.Priority);
        }
    }

    [Fact]
    public async Task Member_Can_Still_Create_Task_With_Normal_Priority()
    {
        // Regression guard: the new check must not block non-URGENT priorities for a Member.
        var world = await Phase3World.CreateAsync();
        await using (world)
        {
            var svc = world.TaskService();

            var task = await svc.CreateTaskAsync(
                title: "Normal-priority task",
                description: null,
                departmentName: world.Dept.Name,
                createdById: world.Member.Id,
                priority: "HIGH");

            Assert.Equal("HIGH", task.Priority);
        }
    }
}

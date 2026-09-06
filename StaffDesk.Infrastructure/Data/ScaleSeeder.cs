using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Data;

/// <summary>OB-10: scale dataset — 200+ employees, 20k tasks over 12 months, leave, reviews, audit events.</summary>
public static class ScaleSeeder
{
    private const int TargetEmployees = 210;
    private const int TargetTasks = 20_000;
    private const int TargetAuditEvents = 2_000;
    private const int TargetLeaveRequests = 400;
    private const int TargetReviews = 180;

    private static readonly string[] TaskStatuses = { "OPEN", "IN_PROGRESS", "IN_REVIEW", "DONE", "BLOCKED", "CANCELLED" };
    private static readonly string[] Priorities = { "LOW", "NORMAL", "HIGH", "URGENT" };
    private static readonly string[] AuditTypes = { "TASK_UPDATED", "TASK_CREATED", "EMPLOYEE_UPDATED", "LOGIN_SUCCESS", "PERMISSION_DENIED" };

    public static async Task<ScaleSeedReport> SeedAsync(AppDbContext context, IAuditService auditService, CancellationToken cancellationToken = default)
    {
        var report = new ScaleSeedReport();

        if (await context.Employees.CountAsync(cancellationToken) >= TargetEmployees)
        {
            report.Skipped = true;
            report.Message = $"Scale seed skipped — already have {await context.Employees.CountAsync(cancellationToken)} employees (target {TargetEmployees}).";
            return report;
        }

        await EnsureDepartmentsAsync(context, cancellationToken);
        await EnsureSeniorityLevelsAsync(context, cancellationToken);

        report.EmployeesAdded = await SeedEmployeesAsync(context, cancellationToken);
        report.TasksAdded = await SeedTasksAsync(context, cancellationToken);
        report.LeaveAdded = await SeedLeaveAsync(context, cancellationToken);
        report.ReviewsAdded = await SeedReviewsAsync(context, cancellationToken);
        report.AuditEventsAdded = await SeedAuditEventsAsync(auditService, cancellationToken);

        report.Message = "Scale seed completed.";
        return report;
    }

    private static async Task EnsureDepartmentsAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.Departments.CountAsync(ct) >= 10) return;
        var names = new[] { "Platform", "Product", "Support", "Legal", "Research" };
        foreach (var name in names)
        {
            if (!await context.Departments.AnyAsync(d => d.Name == name, ct))
                context.Departments.Add(new Department { Name = name, Location = "Remote" });
        }
        await context.SaveChangesAsync(ct);
    }

    private static async Task EnsureSeniorityLevelsAsync(AppDbContext context, CancellationToken ct)
    {
        DbSeeder.SeedSeniorityLevels(context);
        await Task.CompletedTask;
    }

    private static async Task<int> SeedEmployeesAsync(AppDbContext context, CancellationToken ct)
    {
        var existing = await context.Employees.CountAsync(ct);
        var toAdd = TargetEmployees - existing;
        if (toAdd <= 0) return 0;

        var departments = await context.Departments.ToListAsync(ct);
        var levels = await context.SeniorityLevels.OrderBy(l => l.Rank).ToListAsync(ct);
        var managers = await context.Employees.Where(e => e.IsActive).Take(20).ToListAsync(ct);
        var rng = new Random(42);

        var batch = new List<Employee>();
        for (var i = 0; i < toAdd; i++)
        {
            var dept = departments[rng.Next(departments.Count)];
            var level = levels[rng.Next(levels.Count)];
            var manager = managers.Count > 0 ? managers[rng.Next(managers.Count)] : null;
            batch.Add(new Employee
            {
                FullName = $"Scale Employee {existing + i + 1:D4}",
                JobTitle = $"{level.Name} {dept.Name} Specialist",
                DepartmentId = dept.Id,
                LevelId = level.Id,
                ManagerId = manager?.Id,
                IsActive = true
            });

            if (batch.Count >= 100)
            {
                context.Employees.AddRange(batch);
                await context.SaveChangesAsync(ct);
                managers.AddRange(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            context.Employees.AddRange(batch);
            await context.SaveChangesAsync(ct);
        }

        return toAdd;
    }

    private static async Task<int> SeedTasksAsync(AppDbContext context, CancellationToken ct)
    {
        var existing = await context.Tasks.CountAsync(ct);
        var toAdd = TargetTasks - existing;
        if (toAdd <= 0) return 0;

        var employees = await context.Employees.Where(e => e.IsActive).ToListAsync(ct);
        var departments = await context.Departments.ToListAsync(ct);
        var rng = new Random(99);
        var start = DateTime.UtcNow.AddMonths(-12);
        var added = 0;

        while (added < toAdd)
        {
            var chunk = Math.Min(500, toAdd - added);
            var tasks = new List<WorkTask>();
            for (var i = 0; i < chunk; i++)
            {
                var createdAt = start.AddDays(rng.Next(0, 365)).AddHours(rng.Next(0, 24));
                var dept = departments[rng.Next(departments.Count)];
                var creator = employees[rng.Next(employees.Count)];
                var assignee = rng.Next(4) == 0 ? null : employees[rng.Next(employees.Count)];
                var status = TaskStatuses[rng.Next(TaskStatuses.Length)];
                tasks.Add(new WorkTask
                {
                    Key = $"SCALE-{Guid.NewGuid():N}",
                    Title = $"Scale task #{existing + added + i + 1}",
                    Description = "Generated by OB-10 scale seeder.",
                    DepartmentId = dept.Id,
                    CreatedById = creator.Id,
                    AssigneeId = assignee?.Id,
                    Status = status,
                    Priority = Priorities[rng.Next(Priorities.Length)],
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt.AddDays(rng.Next(0, 14)),
                    CompletedAt = status == "DONE" ? createdAt.AddDays(rng.Next(1, 30)) : null,
                    DueAt = createdAt.AddDays(rng.Next(-5, 30))
                });
            }

            context.Tasks.AddRange(tasks);
            await context.SaveChangesAsync(ct);

            foreach (var t in tasks)
                t.Key = $"TSK-{t.Id}";
            await context.SaveChangesAsync(ct);

            added += chunk;
        }

        return added;
    }

    private static async Task<int> SeedLeaveAsync(AppDbContext context, CancellationToken ct)
    {
        var existing = await context.LeaveRequests.CountAsync(ct);
        if (existing >= TargetLeaveRequests) return 0;

        var employees = await context.Employees.Where(e => e.IsActive).ToListAsync(ct);
        var rng = new Random(7);
        var toAdd = TargetLeaveRequests - existing;
        var batch = new List<LeaveRequest>();

        for (var i = 0; i < toAdd; i++)
        {
            var emp = employees[rng.Next(employees.Count)];
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-rng.Next(0, 360)));
            var end = start.AddDays(rng.Next(1, 5));
            var state = rng.Next(10) switch
            {
                < 6 => LeaveStates.Approved,
                < 8 => LeaveStates.Requested,
                < 9 => LeaveStates.Rejected,
                _ => LeaveStates.Cancelled
            };
            batch.Add(new LeaveRequest
            {
                EmployeeId = emp.Id,
                Type = LeaveTypes.All[rng.Next(LeaveTypes.All.Length)],
                StartDate = start,
                EndDate = end,
                State = state,
                CreatedAt = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                UpdatedAt = DateTime.UtcNow
            });
        }

        context.LeaveRequests.AddRange(batch);
        await context.SaveChangesAsync(ct);
        return toAdd;
    }

    private static async Task<int> SeedReviewsAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.PerformanceReviews.CountAsync(ct) >= TargetReviews) return 0;

        var cycle = await context.ReviewCycles.FirstOrDefaultAsync(ct);
        if (cycle == null)
        {
            var admin = await context.Employees.FirstAsync(ct);
            var deptIds = await context.Departments.Select(d => d.Id).ToListAsync(ct);
            cycle = new ReviewCycle
            {
                Name = "Scale FY Review",
                PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12)),
                PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow),
                DepartmentIdsJson = System.Text.Json.JsonSerializer.Serialize(deptIds),
                Stage = ReviewCycleStages.Closed,
                JoinCutOff = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
                CreatedByEmployeeId = admin.Id,
                CreatedAt = DateTime.UtcNow.AddMonths(-11)
            };
            context.ReviewCycles.Add(cycle);
            await context.SaveChangesAsync(ct);
        }

        var employees = await context.Employees.Where(e => e.IsActive).Take(TargetReviews).ToListAsync(ct);
        var existingReviewEmpIds = await context.PerformanceReviews
            .Where(r => r.ReviewCycleId == cycle.Id)
            .Select(r => r.EmployeeId)
            .ToListAsync(ct);

        var toSeed = employees.Where(e => !existingReviewEmpIds.Contains(e.Id)).ToList();
        var rng = new Random(13);
        var reviews = new List<PerformanceReview>();

        foreach (var emp in toSeed)
        {
            var reviewer = emp.ManagerId ?? employees[rng.Next(employees.Count)].Id;
            reviews.Add(new PerformanceReview
            {
                ReviewCycleId = cycle.Id,
                EmployeeId = emp.Id,
                ReviewerEmployeeId = reviewer,
                SelfOverallRating = rng.Next(2, 5),
                SelfSubmitted = true,
                SelfSubmittedAt = DateTime.UtcNow.AddMonths(-2),
                ManagerOverallRating = rng.Next(2, 5),
                ManagerSubmitted = true,
                ManagerSubmittedAt = DateTime.UtcNow.AddMonths(-1),
                CalibratedOverallRating = rng.Next(2, 5),
                CalibratedAt = DateTime.UtcNow.AddDays(-20),
                CreatedAt = DateTime.UtcNow.AddMonths(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-20)
            });
        }

        context.PerformanceReviews.AddRange(reviews);
        await context.SaveChangesAsync(ct);
        return reviews.Count;
    }

    private static async Task<int> SeedAuditEventsAsync(IAuditService auditService, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var added = 0;
        var rng = new Random(17);
        for (var i = 0; i < TargetAuditEvents; i++)
        {
            await auditService.LogAsync(
                AuditTypes[rng.Next(AuditTypes.Length)],
                rng.Next(1, 50),
                "scale-seeder",
                "SUCCESS",
                "Task",
                rng.Next(1, 5000).ToString(),
                requestId: $"scale-{i}");
            added++;
            if (i % 100 == 0)
                await Task.Delay(1, ct);
        }
        return added;
    }
}

public sealed class ScaleSeedReport
{
    public bool Skipped { get; set; }
    public string Message { get; set; } = "";
    public int EmployeesAdded { get; set; }
    public int TasksAdded { get; set; }
    public int LeaveAdded { get; set; }
    public int ReviewsAdded { get; set; }
    public int AuditEventsAdded { get; set; }
}

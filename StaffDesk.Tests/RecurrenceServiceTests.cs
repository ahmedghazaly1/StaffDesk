using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Repositories;
using Xunit;

namespace StaffDesk.Tests;

/// <summary>
/// Regression tests for the "Generate Now produces 0 tasks" bug: a fresh DAILY rule with
/// StartDate = today used to seed occurrence generation from DateTime.UtcNow.Date and then
/// always compute "tomorrow", so its first occurrence was dated a day after StartDate and could
/// never pass GetPendingOccurrencesAsync's "OccurrenceDate &lt;= now" filter on the day the rule
/// was created.
/// </summary>
public class RecurrenceServiceTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static RecurrenceService NewService(AppDbContext db)
    {
        IRecurrenceRepository recurrenceRepo = new RecurrenceRepository(db);
        ITaskRepository taskRepo = new TaskRepository(db, null!);
        IEmployeeRepository employeeRepo = new EmployeeRepository(db);
        IDepartmentRepository departmentRepo = new DepartmentRepository(db);
        return new RecurrenceService(recurrenceRepo, taskRepo, employeeRepo, departmentRepo, null!);
    }

    private static async Task<TaskTemplate> SeedTemplateAsync(AppDbContext db)
    {
        var level = new SeniorityLevel { Name = "Mid", Rank = 30, IsActive = true };
        db.SeniorityLevels.Add(level);
        var dept = new Department { Name = "Engineering", Location = "HQ" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        // EF Core InMemory's Include on a *required* navigation (TaskTemplate.CreatedBy) behaves
        // like an inner join and silently drops the row if the FK target doesn't exist - so
        // CreatedById must point at a real seeded Employee for GetTemplateByIdAsync to find it.
        var creator = new Employee { FullName = "Creator", JobTitle = "Lead", DepartmentId = dept.Id, LevelId = level.Id, IsActive = true };
        db.Employees.Add(creator);
        await db.SaveChangesAsync();

        var template = new TaskTemplate
        {
            Name = "Daily standup",
            TitlePattern = "Standup {date}",
            DefaultPriority = "NORMAL",
            DefaultAssigneeId = null,
            DefaultTags = new List<string>(),
            DefaultChecklistItems = new List<string>(),
            DefaultAcceptanceCriteria = new List<string>(),
            DepartmentId = dept.Id,
            CreatedById = creator.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.TaskTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    [Fact]
    public async Task Daily_Rule_Starting_Today_Generates_An_Occurrence_Dated_Today()
    {
        await using var db = NewDb();
        var template = await SeedTemplateAsync(db);
        var service = NewService(db);

        // Use a fixed, known weekday that is on/before the real clock's "now" rather than the
        // live DateTime.UtcNow.Date: that keeps this test's outcome independent of (a) the
        // unrelated, pre-existing weekend-shift policy in ApplyNonWorkingDayPolicy, which would
        // otherwise push a real weekend "today" to Monday and mask what's being tested, and (b)
        // GetPendingOccurrencesAsync's "OccurrenceDate <= now" filter, which a future-shifted date
        // would fail regardless of this fix.
        var today = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        Assert.True(today <= DateTime.UtcNow);

        var rule = await service.CreateRuleAsync(
            templateId: template.Id,
            frequency: "DAILY",
            daysOfWeek: null,
            dayOfMonth: null,
            startDate: today,
            endDate: today.AddDays(7),
            timezone: "UTC",
            generateOnlyWhenPreviousComplete: false,
            isPaused: false);

        var occurrences = (await service.GetOccurrencesByRuleIdAsync(rule.Id)).OrderBy(o => o.OccurrenceDate).ToList();
        Assert.NotEmpty(occurrences);
        // This is the exact bug: the first occurrence used to be dated tomorrow no matter what
        // StartDate was, so it could never be materialized on the day the rule was created.
        Assert.Equal(today, occurrences[0].OccurrenceDate.Date);

        // The occurrence must actually be pending-and-due now, not starved until tomorrow - this
        // is exactly the filter that swallowed the bug: a "tomorrow"-dated occurrence would not
        // show up here until the next calendar day.
        var pending = await service.GetPendingOccurrencesAsync();
        Assert.Contains(pending, o => o.RuleId == rule.Id && o.OccurrenceDate.Date == today);
    }

    [Fact]
    public async Task Weekly_Rule_Targeting_A_Day_Several_Days_Out_Still_Generates_An_Occurrence()
    {
        // Regression for the GetNextDayOfWeek bug: its loop reset back to the seed date on every
        // miss, so it could never find a target weekday more than 1 day past the seed.
        await using var db = NewDb();
        var template = await SeedTemplateAsync(db);
        var service = NewService(db);

        var today = DateTime.UtcNow.Date;
        // Pick a weekday 4 days from now so the old broken loop (which could only ever land on
        // seed+0 or seed+1) would find nothing. Avoid a weekend target so the (unrelated,
        // pre-existing) weekend-shift policy can't move the occurrence to a different day and
        // mask what this test is actually checking.
        var candidate = today.AddDays(4);
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);
        var targetDay = (int)candidate.DayOfWeek;

        var rule = await service.CreateRuleAsync(
            templateId: template.Id,
            frequency: "WEEKLY",
            daysOfWeek: targetDay.ToString(),
            dayOfMonth: null,
            startDate: today,
            endDate: today.AddDays(30),
            timezone: "UTC",
            generateOnlyWhenPreviousComplete: false,
            isPaused: false);

        var occurrences = (await service.GetOccurrencesByRuleIdAsync(rule.Id)).ToList();
        Assert.NotEmpty(occurrences);
        Assert.All(occurrences, o => Assert.Equal(targetDay, (int)o.OccurrenceDate.DayOfWeek));
    }

    [Fact]
    public async Task Monthly_Rule_Targeting_A_Day_Still_Ahead_This_Month_Lands_In_The_Current_Month()
    {
        // Regression for the GetNextDayOfMonth bug: it always jumped a full month ahead even when
        // the target day-of-month hadn't happened yet in the seed month.
        await using var db = NewDb();
        var template = await SeedTemplateAsync(db);
        var service = NewService(db);

        var today = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var rule = await service.CreateRuleAsync(
            templateId: template.Id,
            frequency: "MONTHLY",
            daysOfWeek: null,
            dayOfMonth: "15",
            startDate: today,
            endDate: today.AddMonths(3),
            timezone: "UTC",
            generateOnlyWhenPreviousComplete: false,
            isPaused: false);

        var occurrences = (await service.GetOccurrencesByRuleIdAsync(rule.Id)).OrderBy(o => o.OccurrenceDate).ToList();
        Assert.NotEmpty(occurrences);
        // First occurrence should land on the 15th of the SAME month (September), not October.
        Assert.Equal(new DateTime(2026, 9, 15), occurrences[0].OccurrenceDate.Date);
    }

    [Fact]
    public async Task StartDate_With_Unspecified_Kind_Is_Treated_As_Utc_Not_Local()
    {
        await using var db = NewDb();
        var template = await SeedTemplateAsync(db);
        var service = NewService(db);

        var unspecified = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
        var rule = await service.CreateRuleAsync(
            templateId: template.Id,
            frequency: "DAILY",
            daysOfWeek: null,
            dayOfMonth: null,
            startDate: unspecified,
            endDate: null,
            timezone: "UTC",
            generateOnlyWhenPreviousComplete: false,
            isPaused: false);

        var stored = await service.GetRuleByIdAsync(rule.Id);
        Assert.NotNull(stored);
        Assert.Equal(unspecified.Date, stored!.StartDate.Date);
    }
}

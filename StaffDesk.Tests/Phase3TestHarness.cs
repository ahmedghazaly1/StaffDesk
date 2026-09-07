using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;
using StaffDesk.Core.Services;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Repositories;
using StaffDesk.Infrastructure.Services;

namespace StaffDesk.Tests;

/// <summary>
/// Shared InMemory EF seed + service factories for Phase 3 backend acceptance tests.
/// Kept permanently — do not delete after a test run.
/// </summary>
internal sealed class Phase3World : IAsyncDisposable
{
    public AppDbContext Db { get; }
    public SeniorityLevel Level { get; private set; } = null!;
    public Department Dept { get; private set; } = null!;
    public Employee Manager { get; private set; } = null!;
    public Employee Member { get; private set; } = null!;
    public Employee Peer { get; private set; } = null!;
    public User ManagerUser { get; private set; } = null!;
    public User MemberUser { get; private set; } = null!;

    /// <summary>A second department, disjoint from Dept, for cross-department permission tests.</summary>
    public Department OtherDept { get; private set; } = null!;
    public Employee OtherDeptMember { get; private set; } = null!;
    public WorkCalendar Calendar { get; private set; } = null!;

    private Phase3World(AppDbContext db) => Db = db;

    public static async Task<Phase3World> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        var world = new Phase3World(db);
        await world.SeedAsync();
        return world;
    }

    private async Task SeedAsync()
    {
        Level = new SeniorityLevel { Name = "Senior", Rank = 40, IsActive = true };
        Db.SeniorityLevels.Add(Level);
        await Db.SaveChangesAsync();

        Calendar = new WorkCalendar
        {
            Name = "Org UTC",
            TimeZoneId = "UTC",
            WorkDaysMask = WorkDayFlags.Weekdays,
            WorkStartHour = 9,
            WorkEndHour = 17,
            IsOrganizationDefault = true,
            EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        Db.WorkCalendars.Add(Calendar);

        Dept = new Department { Name = "Engineering", Location = "HQ" };
        Db.Departments.Add(Dept);
        await Db.SaveChangesAsync();

        Manager = new Employee
        {
            FullName = "Manager One", JobTitle = "Mgr", DepartmentId = Dept.Id,
            LevelId = Level.Id, JoinedAt = new DateOnly(2019, 1, 1), IsActive = true
        };
        Db.Employees.Add(Manager);
        await Db.SaveChangesAsync();

        Dept.ManagerId = Manager.Id;
        await Db.SaveChangesAsync();

        Member = new Employee
        {
            FullName = "Member One", JobTitle = "Dev", DepartmentId = Dept.Id,
            LevelId = Level.Id, ManagerId = Manager.Id, JoinedAt = new DateOnly(2021, 1, 1), IsActive = true
        };
        Peer = new Employee
        {
            FullName = "Peer One", JobTitle = "Dev", DepartmentId = Dept.Id,
            LevelId = Level.Id, ManagerId = Manager.Id, JoinedAt = new DateOnly(2021, 6, 1), IsActive = true
        };
        Db.Employees.AddRange(Member, Peer);
        await Db.SaveChangesAsync();

        var jwt = new JwtService(BuildConfig());
        ManagerUser = new User
        {
            Username = "manager", Email = "m@t", Role = User.Roles.Manager,
            EmployeeId = Manager.Id, PasswordHash = jwt.HashPassword("pass1234")
        };
        MemberUser = new User
        {
            Username = "member", Email = "e@t", Role = User.Roles.Member,
            EmployeeId = Member.Id, PasswordHash = jwt.HashPassword("pass1234")
        };
        Db.Users.AddRange(ManagerUser, MemberUser);
        await Db.SaveChangesAsync();

        OtherDept = new Department { Name = "Marketing", Location = "HQ" };
        Db.Departments.Add(OtherDept);
        await Db.SaveChangesAsync();

        OtherDeptMember = new Employee
        {
            FullName = "Other Dept Member", JobTitle = "Analyst", DepartmentId = OtherDept.Id,
            LevelId = Level.Id, JoinedAt = new DateOnly(2021, 1, 1), IsActive = true
        };
        Db.Employees.Add(OtherDeptMember);
        await Db.SaveChangesAsync();
    }

    public static IConfiguration BuildConfig(Dictionary<string, string?>? extra = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "LocalDev-StaffDesk-Signing-Secret-32ch",
            ["JwtSettings:Issuer"] = "StaffDesk",
            ["JwtSettings:Audience"] = "StaffDeskUsers",
            ["JwtSettings:ExpiryMinutes"] = "15",
            ["Auth:AccessTokenMinutes"] = "15",
            ["Auth:RefreshTokenDays"] = "14",
            ["Auth:LockoutAfterFailures"] = "3",
            ["Auth:LockoutMinutes"] = "15",
            ["Auth:DelayBaseMs"] = "0",
            ["Auth:DelayMaxMs"] = "0"
        };
        if (extra != null)
            foreach (var kv in extra) data[kv.Key] = kv.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    public WorkingCalendarService CalendarService() =>
        new(new WorkCalendarRepository(Db));

    public AuditService Audit() => new(new AuditRepository(Db));

    public TaskRepository Tasks() => new(Db, CalendarService());

    public TaskService TaskService()
    {
        return new TaskService(
            Tasks(),
            new EmployeeRepository(Db),
            new DepartmentRepository(Db),
            new SeniorityLevelRepository(Db),
            new UserRepository(Db),
            new ReworkRepository(Db),
            new StubSla(),
            new ClosureRepository(Db),
            new StubLeaveSvc(),
            new StubTimesheet(),
            Delegation());
    }

    public DelegationService Delegation() =>
        new(new DelegationRepository(Db), new EmployeeRepository(Db), Tasks(), Audit());

    public ApprovalService Approvals() =>
        new(new ApprovalRepository(Db), Tasks(), new EmployeeRepository(Db),
            new DepartmentRepository(Db), Delegation(), Audit(), new UserRepository(Db));

    public TaskRequestService TaskRequests() =>
        new(new TaskRequestRepository(Db), Tasks(), new DepartmentRepository(Db), new UserRepository(Db));

    public LeaveService Leave() =>
        new(new LeaveRepository(Db), new EmployeeRepository(Db), new UserRepository(Db), Audit());

    public CapacityService Capacity() =>
        new(new EmployeeRepository(Db), new DepartmentRepository(Db), new LeaveRepository(Db),
            CalendarService(), Tasks());

    public GovernanceService Governance() =>
        new(new GovernanceRepository(Db), new AuditRepository(Db), Audit(),
            new JobRepository(Db), new EmployeeRepository(Db), Db);

    public AuthSessionService Sessions(IConfiguration? cfg = null)
    {
        cfg ??= BuildConfig();
        return new AuthSessionService(
            new AuthSessionRepository(Db), new UserRepository(Db), new JwtService(cfg), Audit(), cfg);
    }

    public AuthService Auth(IConfiguration? cfg = null)
    {
        cfg ??= BuildConfig();
        return new AuthService(
            new UserRepository(Db), new EmployeeRepository(Db), Sessions(cfg),
            new LoginThrottleRepository(Db), new JwtService(cfg), Audit(), cfg);
    }

    public OperationsService Operations() =>
        new(Db, new JobRepository(Db), new StubAnalyticsRepo(),
            Options.Create(new OperationsOptions()), new NullRequestMetricsCollector(), Audit());

    public SlaService Sla() =>
        new(new SlaRepository(Db), Tasks(), new EmployeeRepository(Db),
            new DepartmentRepository(Db), CalendarService(), Audit());

    public PerformanceService Performance() =>
        new(new PerformanceRepository(Db), Audit(), Tasks());

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    private sealed class StubSla : ISlaService
    {
        public Task<WorkTask> UpdateSlaStateAsync(WorkTask task) => Task.FromResult(task);
        public Task ProcessEscalationsAsync() => Task.CompletedTask;
        public List<(int Level, int DelayMinutes, string Target)> GetEscalationLevels() => new();
        public bool IsBreached(WorkTask task) => false;
        public long? GetRemainingMinutes(WorkTask task) => null;
        public long? GetRemainingMinutes(WorkTask task, int? blockedPauseMinutes) => null;
        public Task<int> GetBlockedPauseMinutesAsync(int taskId) => Task.FromResult(0);
        public Task RecordSlaSnapshotAsync(WorkTask task) => Task.CompletedTask;
        public Task<List<SlaEvaluationResult>> EvaluateAllTasksAsync() => Task.FromResult(new List<SlaEvaluationResult>());
        public Task<SlaTaskState> GetTaskSlaStateAsync(WorkTask task) => Task.FromResult(new SlaTaskState());
        public Task LogSlaEventAsync(int actorId, string eventType, string targetId, string outcome, object? changes = null) => Task.CompletedTask;
    }

    private sealed class StubLeaveSvc : ILeaveService
    {
        public Task<LeaveRequest> RequestLeaveAsync(int employeeId, string type, DateOnly start, DateOnly end, bool partialDay, string? note) => throw new NotImplementedException();
        public Task<LeaveRequest> DecideAsync(int leaveId, int actorEmployeeId, bool approve, string? decisionNote) => throw new NotImplementedException();
        public Task<LeaveRequest> CancelAsync(int leaveId, int actorEmployeeId) => throw new NotImplementedException();
        public Task<IReadOnlyList<object>> ListVisibleAsync(int actorEmployeeId, string actorRole, int? employeeIdFilter = null) =>
            Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
        public Task<IReadOnlyList<LeaveRequest>> GetPendingApprovalsAsync(int managerEmployeeId) =>
            Task.FromResult<IReadOnlyList<LeaveRequest>>(Array.Empty<LeaveRequest>());
        public Task<bool> IsOnApprovedLeaveAsync(int employeeId, DateOnly date) => Task.FromResult(false);
    }

    private sealed class StubTimesheet : ITimesheetService
    {
        public Task<object> GetWeekAsync(int employeeId, DateOnly weekStart, int actorEmployeeId, string actorRole) => throw new NotImplementedException();
        public Task EnsureTimeEntryEditableAsync(int employeeId, DateOnly workedOn) => Task.CompletedTask;
        public Task LinkEntryToWeekAsync(int employeeId, int timeEntryId, DateOnly workedOn) => Task.CompletedTask;
        public Task<Timesheet> SubmitWeekAsync(int employeeId, DateOnly weekStart) => throw new NotImplementedException();
        public Task<Timesheet> ReviewAsync(int timesheetId, int reviewerEmployeeId, bool approve, string? note) => throw new NotImplementedException();
        public Task<Timesheet> ReopenAsync(int timesheetId, int reviewerEmployeeId, string reason) => throw new NotImplementedException();
        public Task<IReadOnlyList<object>> MineAsync(int employeeId) => Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
        public Task<IReadOnlyList<object>> PendingReviewAsync(int managerEmployeeId) => Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
    }

    private sealed class StubAnalyticsRepo : IAnalyticsRepository
    {
        public Task<DailyMetricSnapshot> InsertSnapshotAsync(DailyMetricSnapshot snapshot) => Task.FromResult(snapshot);
        public Task<int> GetLatestVersionAsync(int departmentId, DateOnly date) => Task.FromResult(0);
        public Task<DailyMetricSnapshot?> GetLatestAsync(int departmentId, DateOnly date) => Task.FromResult<DailyMetricSnapshot?>(null);
        public Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeAsync(int departmentId, DateOnly from, DateOnly to) =>
            Task.FromResult<IReadOnlyList<DailyMetricSnapshot>>(Array.Empty<DailyMetricSnapshot>());
        public Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeForDepartmentsAsync(IEnumerable<int> departmentIds, DateOnly from, DateOnly to) =>
            Task.FromResult<IReadOnlyList<DailyMetricSnapshot>>(Array.Empty<DailyMetricSnapshot>());
        public Task<DateTime?> GetNewestComputedAtAsync(int? departmentId = null) => Task.FromResult<DateTime?>(null);
        public Task<IReadOnlyList<WorkTask>> GetTasksForDepartmentAsync(int departmentId) =>
            Task.FromResult<IReadOnlyList<WorkTask>>(Array.Empty<WorkTask>());
        public Task<IReadOnlyList<TaskStatusInterval>> GetIntervalsForTasksAsync(IEnumerable<int> taskIds) =>
            Task.FromResult<IReadOnlyList<TaskStatusInterval>>(Array.Empty<TaskStatusInterval>());
        public Task<IReadOnlyList<TaskRequest>> GetRequestsForDepartmentAsync(int departmentId) =>
            Task.FromResult<IReadOnlyList<TaskRequest>>(Array.Empty<TaskRequest>());
        public Task<IReadOnlyList<ReworkEvent>> GetReworkEventsForTasksAsync(IEnumerable<int> taskIds) =>
            Task.FromResult<IReadOnlyList<ReworkEvent>>(Array.Empty<ReworkEvent>());
        public Task<IReadOnlyList<TaskActivity>> GetAssignmentActivitiesForTasksAsync(IEnumerable<int> taskIds) =>
            Task.FromResult<IReadOnlyList<TaskActivity>>(Array.Empty<TaskActivity>());
        public Task<IReadOnlyList<int>> GetActiveDepartmentIdsAsync() =>
            Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
    }
}

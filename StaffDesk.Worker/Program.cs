using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;
using StaffDesk.Core.Services;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Repositories;
using StaffDesk.Infrastructure.Services;
using StaffDesk.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// OB-5: JSON console logs with scopes. Errors are never sampled.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "o";
    options.UseUtcTimestamp = true;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISeniorityLevelRepository, SeniorityLevelRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<ITaskRequestRepository, TaskRequestRepository>();
builder.Services.AddScoped<ISlaRepository, SlaRepository>();
builder.Services.AddScoped<IApprovalRepository, ApprovalRepository>();
builder.Services.AddScoped<IReworkRepository, ReworkRepository>();
builder.Services.AddScoped<IClosureRepository, ClosureRepository>();
builder.Services.AddScoped<IRecurrenceRepository, RecurrenceRepository>();
builder.Services.AddScoped<IDelegationRepository, DelegationRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<ISavedViewRepository, SavedViewRepository>();
builder.Services.AddScoped<IGovernanceRepository, GovernanceRepository>();
builder.Services.AddScoped<IWorkCalendarRepository, WorkCalendarRepository>();
builder.Services.AddScoped<ILeaveRepository, LeaveRepository>();
builder.Services.AddScoped<ITimesheetRepository, TimesheetRepository>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IPerformanceRepository, PerformanceRepository>();

builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ISeniorityLevelService, SeniorityLevelService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ITaskRequestService, TaskRequestService>();
builder.Services.AddScoped<IWorkingCalendarService, WorkingCalendarService>();
builder.Services.AddScoped<ISlaService, SlaService>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();
builder.Services.AddScoped<IClosureService, ClosureService>();
builder.Services.AddScoped<IRecurrenceService, RecurrenceService>();
builder.Services.AddScoped<IDelegationService, DelegationService>();
builder.Services.AddScoped<IGovernanceService, GovernanceService>();
builder.Services.AddHttpClient("WebhookClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IWebhookDispatchService, WebhookDispatchService>();
builder.Services.AddScoped<ISavedViewService, SavedViewService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<ICapacityService, CapacityService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IPerformanceService, PerformanceService>();
builder.Services.Configure<OperationsOptions>(builder.Configuration.GetSection(OperationsOptions.SectionName));
builder.Services.AddSingleton<IRequestMetricsCollector, NullRequestMetricsCollector>();
builder.Services.AddScoped<IOperationsService, OperationsService>();

// AU-11: CLI verify — `dotnet run --project StaffDesk.Worker -- verify-audit [fromIso] [toIso]`
if (args.Length > 0 && string.Equals(args[0], "verify-audit", StringComparison.OrdinalIgnoreCase))
{
    var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
    var from = args.Length > 1 && DateTime.TryParse(args[1], out var f)
        ? DateTime.SpecifyKind(f, DateTimeKind.Utc)
        : DateTime.UtcNow.AddYears(-10);
    var to = args.Length > 2 && DateTime.TryParse(args[2], out var t)
        ? DateTime.SpecifyKind(t, DateTimeKind.Utc)
        : DateTime.UtcNow.AddDays(1);

    var (isValid, firstBreak) = await audit.VerifyChainAsync(from, to);
    Console.WriteLine(isValid
        ? $"AUDIT CHAIN OK for {from:o} .. {to:o}"
        : $"AUDIT CHAIN BROKEN at event id {firstBreak} for {from:o} .. {to:o}");
    Environment.Exit(isValid ? 0 : 2);
    return;
}

// AN-5: CLI rebuild — `dotnet run --project StaffDesk.Worker -- rebuild-metrics yyyy-MM-dd yyyy-MM-dd [departmentId]`
if (args.Length > 0 && string.Equals(args[0], "rebuild-metrics", StringComparison.OrdinalIgnoreCase))
{
    var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
    if (args.Length < 3
        || !DateOnly.TryParse(args[1], out var from)
        || !DateOnly.TryParse(args[2], out var to))
    {
        Console.Error.WriteLine("Usage: rebuild-metrics <from yyyy-MM-dd> <to yyyy-MM-dd> [departmentId]");
        Environment.Exit(1);
        return;
    }
    int? deptId = args.Length > 3 && int.TryParse(args[3], out var d) ? d : null;
    var n = await analytics.RebuildAsync(from, to, deptId);
    Console.WriteLine($"METRIC REBUILD wrote {n} snapshot(s) for {from:yyyy-MM-dd} .. {to:yyyy-MM-dd}");
    Environment.Exit(0);
    return;
}

// OB-10: scale seed — `dotnet run --project StaffDesk.Worker -- seed-scale`
if (args.Length > 0 && string.Equals(args[0], "seed-scale", StringComparison.OrdinalIgnoreCase))
{
    var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
    var report = await ScaleSeeder.SeedAsync(db, audit);
    Console.WriteLine(report.Skipped ? report.Message : $"{report.Message} employees+{report.EmployeesAdded} tasks+{report.TasksAdded} leave+{report.LeaveAdded} reviews+{report.ReviewsAdded} audit+{report.AuditEventsAdded}");
    Environment.Exit(report.Skipped ? 0 : 0);
    return;
}

builder.Services.AddHostedService<JobWorker>();

var workerHost = builder.Build();
workerHost.Run();

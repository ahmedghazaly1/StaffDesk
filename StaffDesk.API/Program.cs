using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;
using StaffDesk.Infrastructure.Data;
using StaffDesk.Infrastructure.Repositories;
using StaffDesk.Infrastructure.Services;
using StaffDesk.API;
using StaffDesk.API.Filters;
using StaffDesk.API.Common;
using StaffDesk.API.Middleware;
using StaffDesk.API.Metrics;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// OB-5: JSON console logs with scopes (RequestId). No log sampling — errors are never dropped.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "o";
    options.UseUtcTimestamp = true;
});

// Add services to the container
// Employee is self-referencing (Manager/DirectReports), and EF Core auto-fixes-up navigation
// properties between ANY tracked entities that share a FK - even ones never .Include()'d - once
// more than one related Employee is loaded into the same DbContext (e.g. two AuditEvents whose
// actors are manager/report of each other). IgnoreCycles is a safety net for that, app-wide.
// PL-13: HttpClient for outbound webhook delivery; configured with a 10s timeout.
builder.Services.AddHttpClient("WebhookClient", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddControllers(options =>
{
    options.Filters.AddService<FieldRedactionResultFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StaffDesk API",
        Version = "v1",
        Description = "Phase 3 API. Errors use { error: { code, message, details, requestId } }. Rate limits: 429 + Retry-After + X-RateLimit-*. Concurrency: ETag / If-Match (412/428)."
    });
    c.OperationFilter<OpenApiConventionsFilter>();
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGciOiJIUzI1NiIs..."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Register DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================
// Register Repositories
// ============================================
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

// ============================================
// Register Services
// ============================================
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
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
builder.Services.AddScoped<IGovernanceService, StaffDesk.Infrastructure.Services.GovernanceService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IWebhookDispatchService, WebhookDispatchService>();
builder.Services.AddScoped<ISavedViewService, SavedViewService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<ICapacityService, CapacityService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IPerformanceRepository, PerformanceRepository>();
builder.Services.AddScoped<IPerformanceService, PerformanceService>();
builder.Services.AddScoped<IOperationsService, OperationsService>();
builder.Services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
builder.Services.AddScoped<ILoginThrottleRepository, LoginThrottleRepository>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddScoped<FieldRedactionResultFilter>();

builder.Services.Configure<OperationsOptions>(builder.Configuration.GetSection(OperationsOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
var rateLimits = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddSingleton<IRequestMetricsCollector, RequestMetricsCollector>();

// Background jobs run in StaffDesk.Worker (AR-5).

// SP-11: signing secret from environment only — refuse empty or the known committed default.
var envSecret = Environment.GetEnvironmentVariable("STAFFDESK_JWT_SECRET");
if (!string.IsNullOrWhiteSpace(envSecret))
    builder.Configuration["JwtSettings:SecretKey"] = envSecret;

const string BannedDefaultSecret = "YourSuperSecretKeyForStaffDesk2026!@#$%^&*()_+";
var secretKey = builder.Configuration["JwtSettings:SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey)
    || secretKey == BannedDefaultSecret
    || Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException(
        "STAFFDESK_JWT_SECRET must be set to a non-default value of at least 32 characters. StaffDesk will not start with an empty or known-default signing secret (SP-11).");
}

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var issuer = jwtSettings["Issuer"] ?? "StaffDesk";
var audience = jwtSettings["Audience"] ?? "StaffDeskUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

// ============================================
// PL-9/PL-10 / NFR-23: rate limiting from RateLimits config (not hard-coded).
// ============================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        var retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);

        context.HttpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        var p = context.HttpContext.Request.Path.Value ?? "";
        var m = context.HttpContext.Request.Method;
        var limitShown = p.Contains("/auth/login", StringComparison.OrdinalIgnoreCase) ? rateLimits.AuthPerMinute
            : (p.Contains("/exports", StringComparison.OrdinalIgnoreCase) || p.Contains("/data-export", StringComparison.OrdinalIgnoreCase) || p.Contains("/erasure", StringComparison.OrdinalIgnoreCase)) ? rateLimits.ExportPerMinute
            : (HttpMethods.IsGet(m) || HttpMethods.IsHead(m) || HttpMethods.IsOptions(m)) ? rateLimits.ReadPerMinute
            : rateLimits.WritePerMinute;
        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = limitShown.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
        context.HttpContext.Response.ContentType = "application/json";
        try
        {
            await using var scope = context.HttpContext.RequestServices.CreateAsyncScope();
            var audit = scope.ServiceProvider.GetService<IAuditService>();
            if (audit != null)
            {
                await audit.LogAsync(
                    "RATE_LIMIT_TRIGGERED",
                    null,
                    "system",
                    "DENIED",
                    "HttpRequest",
                    p,
                    requestId: context.HttpContext.TraceIdentifier,
                    sourceIp: context.HttpContext.Connection.RemoteIpAddress?.ToString());
            }
        }
        catch { /* never fail the 429 path */ }
        await context.HttpContext.Response.WriteAsJsonAsync(ApiError.Build(
            "RATE_LIMIT_EXCEEDED",
            "Too many requests. Slow down and try again later.",
            new List<string> { $"retryAfterSeconds:{retryAfterSeconds}" }), cancellationToken: token);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path.Value ?? "";
        var method = httpContext.Request.Method;

        // OB-1/OB-3: probes and metrics scrapes must not consume user rate limits.
        if (path.Contains("/health", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("probe");
        }

        var principal = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var partyKey = principal ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.AuthPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }

        if ((HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
            && (path.Contains("/exports", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/data-export", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/erasure", StringComparison.OrdinalIgnoreCase)))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"export:{partyKey}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.ExportPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }

        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"read:{partyKey}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.ReadPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }

        return RateLimitPartition.GetFixedWindowLimiter($"write:{partyKey}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimits.WritePerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// OB-5: request id + logging scope must wrap the rest of the pipeline so exception
// handler and later middleware lines carry RequestId.
app.UseMiddleware<RequestIdLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// VE-2: process-wide safety net for anything that escapes the per-controller TaskDomainException
// catches - logs the real exception server-side but never leaks it to the client.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        if (feature?.Error != null)
        {
            logger.LogError(feature.Error, "Unhandled exception on {Method} {Path} RequestId={RequestId}",
                context.Request.Method, context.Request.Path, RequestIdLoggingMiddleware.GetRequestId(context));
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(ApiError.Build(
            StaffDesk.Core.Exceptions.TaskErrorCodes.InternalError,
            "An unexpected error occurred."));
    });
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();

// PL-15: Deprecation/Sunset headers on any path registered in DeprecationMiddleware.Registry.
app.UseMiddleware<DeprecationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SessionRevocationMiddleware>();

// PL-9/PL-10: after auth, so authenticated requests partition by principal rather than falling
// back to IP; login itself partitions by IP regardless (see GlobalLimiter above).
app.UseRateLimiter();

// OB-3/OB-5: structured request metrics and logging (after rate limiter, before idempotency).
app.UseMiddleware<RequestMetricsMiddleware>();

// ============================================
// Idempotency Middleware - PL-1..PL-3, must run before Audit so a replayed response never
// produces a second audit event for the same original mutation.
// ============================================
app.UseMiddleware<IdempotencyMiddleware>();

// ============================================
// Audit Middleware - logs all requests
// ============================================
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
    
    // ============================================
    // Phase 2: Seed SeniorityLevels FIRST
    // ============================================
    DbSeeder.SeedSeniorityLevels(dbContext);
    DbSeeder.SeedCompetencies(dbContext);
    
    // ============================================
    // Phase 1: Seed Departments, Employees, Users
    // ============================================
    DbSeeder.Seed(dbContext);
    DbSeeder.SeedUsers(dbContext);
    DbSeeder.SeedTasks(dbContext);

    // WC-26: one-time backfill of TaskStatusInterval rows for tasks that predate this feature.
    TaskStatusIntervalBackfill.Backfill(dbContext);

    // DG-1: seed retention policies from configuration defaults when empty.
    var govRepo = scope.ServiceProvider.GetRequiredService<IGovernanceRepository>();
    await govRepo.SeedRetentionPoliciesIfEmptyAsync(new[]
    {
        new RetentionPolicy { DataClass = RetentionDataClasses.AuditEvents, RetentionDays = 2555, Description = "Security audit trail (~7 years)" },
        new RetentionPolicy { DataClass = RetentionDataClasses.TaskActivity, RetentionDays = 730, Description = "Task activity log" },
        new RetentionPolicy { DataClass = RetentionDataClasses.Notifications, RetentionDays = 180, Description = "In-app notifications" },
        new RetentionPolicy { DataClass = RetentionDataClasses.SoftDeletedTasks, RetentionDays = 365, Description = "Soft-deleted tasks before hard delete" },
        new RetentionPolicy { DataClass = RetentionDataClasses.SessionRecords, RetentionDays = 90, Description = "Session / refresh records (when introduced)" },
        new RetentionPolicy { DataClass = RetentionDataClasses.IdempotencyKeys, RetentionDays = 1, Description = "PL-3: idempotency replay records (purged against their own ExpiresAt, not this RetentionDays value)" },
    });

    // CP-1: seed organization default work calendar when empty.
    var calendarRepo = scope.ServiceProvider.GetRequiredService<IWorkCalendarRepository>();
    await calendarRepo.SeedDefaultIfEmptyAsync();

    // Repair audit hashes written before OccurredAt was truncated to Postgres
    // microsecond precision (false "chain broken at event id 1" failures).
    var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditRepository>();
    var from = DateTime.UtcNow.AddYears(-20);
    var to = DateTime.UtcNow.AddDays(1);
    var (chainOk, _) = await auditRepo.VerifyChainAsync(from, to);
    if (!chainOk)
    {
        var repaired = await auditRepo.RebuildChainHashesAsync();
        Console.WriteLine($"Audit chain rehashed ({repaired} event hash(es) updated after microsecond-precision fix).");
    }
}

app.Run();
using System.Text;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Middleware;

public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService, IUserRepository userRepository)
    {
        // Skip audit for non-authenticated endpoints (login, health, etc.) - login is audited
        // directly from AuthController instead, since there is no authenticated actor yet here.
        var path = context.Request.Path.Value ?? "";
        if (path.Contains("/auth/login") ||
            path.Contains("/health") ||
            path.Contains("/metrics") ||
            path.Contains("/swagger"))
        {
            await _next(context);
            return;
        }
        var method = context.Request.Method;

        // AU-15: AUDITOR is read-only over the audit log and nothing else - no task, employee, or
        // any other business content. Enforced here, once, rather than teaching every controller
        // about a role it was never designed around (PR-4's "one reusable layer" principle).
        // Auth session endpoints are allowed so the auditor can stay signed in and sign out;
        // they do not expose business data.
        if (context.User.IsInRole("Auditor")
            && !path.Contains("/audit/", StringComparison.OrdinalIgnoreCase)
            && !IsAuditorAuthPath(path))
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            var actorIdForDenial = await GetActorEmployeeIdAsync(context, userRepository);
            await TryLogAsync(auditService, "PERMISSION_DENIED", actorIdForDenial, GetActorLabel(context), "DENIED",
                GetTargetInfo(path), context.TraceIdentifier, GetClientIp(context), context.Request.Headers.UserAgent.ToString(), null, _logger);
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "FORBIDDEN",
                    message = "The AUDITOR role has no access outside the audit log.",
                    details = Array.Empty<string>(),
                    requestId = context.TraceIdentifier
                }
            });
            return;
        }

        // An audit trail records who changed what, not every read - auditing GET/HEAD too meant a
        // single UI action (e.g. creating a task, which first GETs dropdown data) produced a burst
        // of unrelated rows, and every one of those reads fell through the classifier below to the
        // misleading "PERMISSION_DENIED" default even though nothing was denied. AU-4 carves out
        // one exception: reads of the audit log itself are sensitive and ARE audited.
        if (method == "GET" || method == "HEAD" || method == "OPTIONS")
        {
            await _next(context);

            if (path.Contains("/audit/", StringComparison.OrdinalIgnoreCase))
            {
                var readerActorId = await GetActorEmployeeIdAsync(context, userRepository);
                var readOutcome = context.Response.StatusCode >= 400 ? "DENIED" : "SUCCESS";
                await TryLogAsync(auditService, "AUDIT_LOG_READ", readerActorId, GetActorLabel(context), readOutcome,
                    GetTargetInfo(path), context.TraceIdentifier, GetClientIp(context), context.Request.Headers.UserAgent.ToString(), null, _logger);
            }
            // AU-4: reads of another employee's time entries are sensitive
            else if (path.Contains("/time-entries", StringComparison.OrdinalIgnoreCase)
                     && context.Response.StatusCode < 400)
            {
                var readerActorId = await GetActorEmployeeIdAsync(context, userRepository);
                await TryLogAsync(auditService, "TIME_ENTRIES_READ", readerActorId, GetActorLabel(context), "SUCCESS",
                    GetTargetInfo(path), context.TraceIdentifier, GetClientIp(context), context.Request.Headers.UserAgent.ToString(), null, _logger);
            }
            else if (context.Response.StatusCode == 403
                     || (context.Response.StatusCode == 404 && IsHiddenRecordPath(path)))
            {
                var denierActorId = await GetActorEmployeeIdAsync(context, userRepository);
                await TryLogAsync(auditService, "PERMISSION_DENIED", denierActorId, GetActorLabel(context), "DENIED",
                    GetTargetInfo(path), context.TraceIdentifier, GetClientIp(context), context.Request.Headers.UserAgent.ToString(),
                    new { status = context.Response.StatusCode, method, path }, _logger);
            }
            return;
        }

        // AuditEvent.Actor is FK'd to Employee, but the JWT's NameIdentifier claim is User.Id -
        // resolve the real Employee.Id the same way the rest of the app does, so the embedded
        // actor doesn't silently point at an unrelated employee that happens to share the id.
        var actorId = await GetActorEmployeeIdAsync(context, userRepository);
        var actorLabel = GetActorLabel(context);
        var requestId = context.TraceIdentifier;
        var sourceIp = GetClientIp(context);
        var userAgent = context.Request.Headers.UserAgent.ToString();

        // Read the request body for audit (POST, PUT, PATCH, DELETE-with-body)
        string? requestBody = null;
        context.Request.EnableBuffering();
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
        }
        context.Request.Body.Position = 0;

        // Capture the original response body
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        Exception? handlerException = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            handlerException = ex;
        }

        // Outcome reflects the actual HTTP result, not just whether a .NET exception was thrown -
        // a 401/403 from [Authorize]/[AdminOnly] never throws, it just sets the status code.
        var outcome = handlerException != null || context.Response.StatusCode >= 400 ? "DENIED" : "SUCCESS";

        // AU-6: audit is not best-effort (deliberately the opposite of the notification rule,
        // NT-3). If the audit write itself fails, the caller must not receive a silent success -
        // this propagates instead of swallowing. It cannot roll back whatever the handler already
        // did to the database (that would need every controller wrapped in a shared transaction
        // with the audit write, a much larger change), but it does turn an un-audited success into
        // a visible failure rather than a silent one.
        try
        {
            await WriteAuditEventAsync(context, auditService, actorId, actorLabel, requestId, sourceIp, userAgent, requestBody, outcome);
        }
        catch (Exception auditEx)
        {
            _logger.LogError(auditEx, "Audit write failed for request {RequestId} - withholding the action's result from the caller", requestId);
            context.Response.Body = originalBodyStream;
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "AUDIT_WRITE_FAILED",
                        message = "This action could not be completed because it could not be recorded in the audit log.",
                        details = Array.Empty<string>(),
                        requestId
                    }
                });
            }
            return;
        }

        context.Response.Body = originalBodyStream;

        if (handlerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(handlerException).Throw();
        }

        responseBodyStream.Seek(0, SeekOrigin.Begin);
        await responseBodyStream.CopyToAsync(originalBodyStream);
    }

    private async Task WriteAuditEventAsync(
        HttpContext context,
        IAuditService auditService,
        int? actorId,
        string actorLabel,
        string requestId,
        string sourceIp,
        string userAgent,
        string? requestBody,
        string outcome)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";

        var eventType = GetEventType(method, path, outcome);
        var targetInfo = GetTargetInfo(path);

        object? changes = null;
        if (!string.IsNullOrEmpty(requestBody) &&
            (method == "POST" || method == "PUT" || method == "PATCH"))
        {
            try
            {
                changes = System.Text.Json.JsonSerializer.Deserialize<object>(requestBody);
            }
            catch
            {
                changes = new { Raw = requestBody };
            }
        }

        await auditService.LogAsync(
            eventType: eventType,
            actorId: actorId,
            actorLabel: actorLabel,
            outcome: outcome,
            targetType: targetInfo.Type,
            targetId: targetInfo.Id,
            requestId: requestId,
            sourceIp: sourceIp,
            userAgent: userAgent,
            changes: changes
        );
    }

    // Used only for the two read-side audit cases (AU-4 audit-log reads, AU-15 auditor denials),
    // which are best-effort by design (a failed audit of a READ shouldn't turn every accidental
    // touch of the audit log into a 500 - AU-6's "must not fail silently" is about mutations).
    private static async Task TryLogAsync(
        IAuditService auditService, string eventType, int? actorId, string actorLabel, string outcome,
        (string? Type, string? Id) targetInfo, string requestId, string sourceIp, string userAgent,
        object? changes, ILogger logger)
    {
        try
        {
            await auditService.LogAsync(eventType, actorId, actorLabel, outcome, targetInfo.Type, targetInfo.Id,
                requestId: requestId, sourceIp: sourceIp, userAgent: userAgent, changes: changes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write a read-side audit event for request {RequestId}", requestId);
        }
    }

    private static bool IsHiddenRecordPath(string path)
    {
        var p = path.ToLowerInvariant();
        return p.Contains("/reviews") || p.Contains("/leave") || p.Contains("/goals")
            || p.Contains("/peer-feedback") || p.Contains("/calibration") || p.Contains("/feedback-notes");
    }

    private async Task<int?> GetActorEmployeeIdAsync(HttpContext context, IUserRepository userRepository)
    {
        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            return null;

        var user = await userRepository.GetByIdAsync(userId);
        return user?.EmployeeId;
    }

    private string GetActorLabel(HttpContext context)
    {
        var nameClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Name);
        var roleClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
        var name = nameClaim?.Value ?? "Anonymous";
        var role = roleClaim?.Value ?? "Unknown";
        return $"{name} ({role})";
    }

    private string GetClientIp(HttpContext context)
    {
        // Check for forwarded IP headers
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string GetEventType(string method, string path, string outcome)
    {
        if (outcome == "DENIED")
            return "PERMISSION_DENIED";

        var lowerPath = path.ToLower();
        var methodLower = method.ToLower();

        if (lowerPath.Contains("/auth/login")) return "LOGIN_SUCCEEDED";
        if (lowerPath.Contains("/auth/logout")) return "LOGOUT";
        if (lowerPath.Contains("/auth/refresh")) return "TOKEN_REFRESHED";
        if (lowerPath.Contains("/auth/password")) return "PASSWORD_CHANGED";

        if (lowerPath.Contains("/employees"))
        {
            if (methodLower == "post") return "EMPLOYEE_CREATED";
            if (methodLower == "put" || methodLower == "patch") return "EMPLOYEE_UPDATED";
            if (methodLower == "delete") return "EMPLOYEE_DEACTIVATED";
        }

        if (lowerPath.Contains("/departments"))
        {
            if (methodLower == "post" || methodLower == "put" || methodLower == "patch") return "EMPLOYEE_UPDATED";
        }

        if (lowerPath.Contains("/task-requests"))
        {
            if (lowerPath.Contains("decline")) return "REQUEST_DECLINED";
            if (lowerPath.Contains("accept") || lowerPath.Contains("triage")) return "REQUEST_TRIAGED";
            if (methodLower == "post") return "REQUEST_SUBMITTED";
        }

        if (lowerPath.Contains("/sla"))
        {
            if (methodLower == "post") return "SLA_POLICY_CREATED";
            if (methodLower == "put" || methodLower == "patch") return "SLA_POLICY_CHANGED";
            if (methodLower == "delete") return "SLA_POLICY_CHANGED";
        }

        if (lowerPath.Contains("/delegations"))
        {
            if (methodLower == "post") return "DELEGATION_CREATED";
            if (methodLower == "delete") return "DELEGATION_ENDED";
        }

        if (lowerPath.Contains("/leave"))
        {
            if (methodLower == "post") return "LEAVE_REQUESTED";
            if (methodLower == "put" || methodLower == "patch") return "LEAVE_DECIDED";
        }

        if (lowerPath.Contains("/reviews") || lowerPath.Contains("/review-cycles") || lowerPath.Contains("/goals"))
        {
            if (methodLower == "post") return "REVIEW_UPDATED";
            if (methodLower == "put" || methodLower == "patch") return "REVIEW_UPDATED";
        }

        if (lowerPath.Contains("/webhooks"))
        {
            if (methodLower == "post") return "WEBHOOK_CREATED";
            if (methodLower == "delete") return "WEBHOOK_DISABLED";
        }

        if (lowerPath.Contains("/tasks"))
        {
            if (methodLower == "post") return "TASK_CREATED";
            if (methodLower == "put" || methodLower == "patch")
            {
                if (lowerPath.Contains("/status")) return "TASK_STATUS_CHANGED";
                if (lowerPath.Contains("/assignee")) return "TASK_ASSIGNEE_CHANGED";
                return "TASK_UPDATED";
            }
            if (methodLower == "delete") return "TASK_DELETED";
        }

        if (lowerPath.Contains("/seniority-levels"))
        {
            if (methodLower == "post" || methodLower == "put" || methodLower == "patch" || methodLower == "delete")
                return "LEVEL_CHANGED";
        }

        if (lowerPath.Contains("/permissions") || lowerPath.Contains("/roles"))
        {
            if (methodLower == "post" || methodLower == "put") return "ROLE_GRANTED";
            if (methodLower == "delete") return "ROLE_REVOKED";
        }

        return $"{methodLower.ToUpperInvariant()}_{(lowerPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "UNKNOWN").ToUpperInvariant()}";
    }

    private (string? Type, string? Id) GetTargetInfo(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Try to extract entity type and ID from path
        // e.g., /v1/tasks/123 -> Task, 123
        // /v1/employees/456 -> Employee, 456
        
        if (parts.Length >= 3)
        {
            var entityType = parts[1] switch
            {
                "tasks" => "Task",
                "employees" => "Employee",
                "departments" => "Department",
                "seniority-levels" => "SeniorityLevel",
                "users" => "User",
                _ => null
            };

            if (entityType != null && int.TryParse(parts[2], out int id))
            {
                return (entityType, id.ToString());
            }

            // Try to find the ID in the path
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int parsedId))
                {
                    return (entityType ?? "Resource", parsedId.ToString());
                }
            }
        }

        return (null, null);
    }

    private static bool IsAuditorAuthPath(string path) =>
        path.Contains("/auth/logout", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/auth/password", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/auth/sessions", StringComparison.OrdinalIgnoreCase);
}
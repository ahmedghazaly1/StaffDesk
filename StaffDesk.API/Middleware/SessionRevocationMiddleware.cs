using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Middleware;

/// <summary>SP-2: revoked sessions are rejected on the next request.</summary>
public class SessionRevocationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionRevocationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuthSessionService sessions)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/v1/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/v1/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/v1/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sid = context.User.FindFirst("sid")?.Value;
            if (!Guid.TryParse(sid, out var sessionId) || !await sessions.IsSessionActiveAsync(sessionId))
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "UNAUTHORIZED",
                        message = "Session is no longer valid.",
                        details = Array.Empty<string>(),
                        requestId = context.TraceIdentifier
                    }
                });
                return;
            }
        }

        await _next(context);
    }
}

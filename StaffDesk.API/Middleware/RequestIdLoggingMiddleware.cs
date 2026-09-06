namespace StaffDesk.API.Middleware;

/// <summary>
/// OB-5: every request gets a request id on the response and in the logging scope,
/// so every subsequent log line in this request carries RequestId.
/// </summary>
public class RequestIdLoggingMiddleware
{
    public const string HeaderName = "X-Request-Id";
    public const string ItemKey = "StaffDesk.RequestId";

    private readonly RequestDelegate _next;

    public RequestIdLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<RequestIdLoggingMiddleware> logger)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var requestId = string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString() : incoming.Trim();
        context.Items[ItemKey] = requestId;
        context.Response.Headers[HeaderName] = requestId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.Value ?? "/"
        }))
        {
            await _next(context);
        }
    }

    public static string GetRequestId(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var value) && value is string s && !string.IsNullOrEmpty(s))
            return s;
        return context.TraceIdentifier;
    }
}

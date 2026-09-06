using System.Diagnostics;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.API.Middleware;

/// <summary>OB-3/OB-5: records per-route latency for Prometheus and emits structured request logs.</summary>
public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestMetricsMiddleware> _logger;

    public RequestMetricsMiddleware(RequestDelegate next, ILogger<RequestMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRequestMetricsCollector metrics)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var route = NormalizeRoute(context.Request.Method, context.Request.Path.Value ?? "/");
            metrics.RecordRequest(route, context.Response.StatusCode, sw.Elapsed.TotalMilliseconds);

            // OB-5: request id is already on the logging scope; never sample errors.
            var requestId = RequestIdLoggingMiddleware.GetRequestId(context);
            if (context.Response.StatusCode >= 500)
            {
                _logger.LogError(
                    "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs}ms RequestId={RequestId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    sw.Elapsed.TotalMilliseconds,
                    requestId);
            }
            else
            {
                _logger.LogInformation(
                    "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs}ms RequestId={RequestId}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    sw.Elapsed.TotalMilliseconds,
                    requestId);
            }
        }
    }

    internal static string NormalizeRoute(string method, string path)
    {
        // Collapse numeric ids so metrics cardinality stays bounded.
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (int.TryParse(segments[i], out _) || long.TryParse(segments[i], out _))
                segments[i] = "{id}";
        }
        return method + " /" + string.Join('/', segments);
    }
}

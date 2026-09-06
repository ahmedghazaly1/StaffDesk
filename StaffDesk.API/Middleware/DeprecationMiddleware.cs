namespace StaffDesk.API.Middleware;

// PL-15: Injects Deprecation and Sunset headers on any path that has been marked as deprecated.
// Per RFC 8594 (Sunset) and draft-ietf-httpapi-deprecation-header:
//   Deprecation: <HTTP-date of when deprecation was announced>
//   Sunset:      <HTTP-date of when the endpoint will stop responding>
// Add entries to the registry below when deprecating an endpoint. Never remove an entry until
// the Sunset date has passed and the endpoint has been removed.
public class DeprecationMiddleware
{
    private static readonly Dictionary<string, DeprecationEntry> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        // Example entry (uncomment and update when a real deprecation is declared):
        // ["/v1/old-endpoint"] = new DeprecationEntry(
        //     DeprecationDate: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        //     SunsetDate:      new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero),
        //     Link:            "https://docs.staffdesk.internal/changelog#2026-09-01-deprecation"),
    };

    private readonly RequestDelegate _next;

    public DeprecationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Prefix-match so /v1/old-endpoint and /v1/old-endpoint/123 both get the headers.
        var match = Registry.Keys.FirstOrDefault(k =>
            path.StartsWith(k, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            var entry = Registry[match];
            // RFC 8594 dates use IMF-fixdate format.
            context.Response.Headers["Deprecation"] = entry.DeprecationDate.ToString("R");
            context.Response.Headers["Sunset"] = entry.SunsetDate.ToString("R");
            if (!string.IsNullOrEmpty(entry.Link))
                context.Response.Headers["Link"] = $"<{entry.Link}>; rel=\"deprecation\"";
        }

        await _next(context);
    }

    private record DeprecationEntry(
        DateTimeOffset DeprecationDate,
        DateTimeOffset SunsetDate,
        string? Link);
}

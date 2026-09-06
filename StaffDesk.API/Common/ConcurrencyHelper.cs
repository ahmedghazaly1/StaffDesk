namespace StaffDesk.API.Common;

// Part F - PL-4..PL-7: optimistic concurrency for Task, Employee, PerformanceReview, and Goal.
// The ETag is derived from UpdatedAt (a version column would work equally well; UpdatedAt is
// already maintained on every write to these entities, so it costs nothing new to compute from).
public static class ConcurrencyHelper
{
    public static string ComputeETag(DateTime updatedAt) => $"\"{updatedAt.Ticks:x}\"";

    /// <summary>
    /// PL-5/PL-6: If-Match is required on these endpoints (no legacy clients predate this feature,
    /// so the BC-3 deprecation window that would otherwise make it optional-then-required is moot
    /// here - it is required from the start). Returns null when the header matches; otherwise the
    /// (status, body) pair the caller should return immediately.
    /// </summary>
    public static (int Status, object Body)? RequireIfMatch(HttpRequest request, DateTime currentUpdatedAt)
    {
        var currentETag = ComputeETag(currentUpdatedAt);
        var ifMatch = request.Headers.IfMatch.ToString();

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return (428, ApiError.Build(
                StaffDesk.Core.Exceptions.TaskErrorCodes.PreconditionRequired,
                "An If-Match header is required to update this resource. GET it first to obtain the current ETag.",
                new List<string> { $"currentETag:{currentETag}" }));
        }

        if (!Matches(ifMatch, currentETag))
        {
            // PL-7: the current ETag is returned so the caller can re-fetch, merge, and retry
            // without a second round trip just to learn what the current value is.
            return (412, ApiError.Build(
                StaffDesk.Core.Exceptions.TaskErrorCodes.PreconditionFailed,
                "The resource has changed since you last read it. Re-fetch and retry.",
                new List<string> { $"currentETag:{currentETag}" }));
        }

        return null;
    }

    public static void SetETag(HttpResponse response, DateTime updatedAt) =>
        response.Headers.ETag = ComputeETag(updatedAt);

    private static bool Matches(string ifMatchHeader, string currentETag)
    {
        // If-Match may carry one or more comma-separated ETags, or "*".
        if (ifMatchHeader.Trim() == "*") return true;
        return ifMatchHeader.Split(',')
            .Select(t => t.Trim())
            .Any(t => t == currentETag);
    }
}

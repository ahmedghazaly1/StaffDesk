namespace StaffDesk.Core;

/// <summary>
/// PostgreSQL <c>timestamp</c> stores microsecond precision only. .NET
/// <see cref="DateTime"/> has 100-nanosecond ticks, so values written with
/// sub-microsecond digits round-trip to a slightly different instant. Audit
/// hashes that include <c>OccurredAt</c> must always use this precision.
/// </summary>
public static class AuditTime
{
    public static DateTime TruncateToMicroseconds(DateTime value)
        => new((value.Ticks / 10) * 10, value.Kind);
}

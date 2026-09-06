namespace StaffDesk.Core.Models;

/// <summary>AN-1 reporting shape: duration metrics as p50/p85/p95 with n.</summary>
public sealed class PercentileResult
{
    public int N { get; init; }
    public double? P50 { get; init; }
    public double? P85 { get; init; }
    public double? P95 { get; init; }
    public bool IncludesBackfilled { get; init; }
}

public sealed class RatioResult
{
    public int Numerator { get; init; }
    public int Denominator { get; init; }
    public double? Value => Denominator == 0 ? null : Math.Round(100.0 * Numerator / Denominator, 1);
}

public static class MetricBasis
{
    public const string WorkingMinutes = "working_minutes";
    public const string ElapsedMinutes = "elapsed_minutes";
}

public static class InsufficientData
{
    public const string Marker = "insufficient_data";
    public const int DefaultThreshold = 5;
}

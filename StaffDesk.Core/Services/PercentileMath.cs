using StaffDesk.Core.Models;

namespace StaffDesk.Core.Services;

/// <summary>Pure percentile helpers — unit-tested with hand-computed fixtures (AN-1).</summary>
public static class PercentileMath
{
    public static PercentileResult FromSorted(IReadOnlyList<double> sortedAscending, bool includesBackfilled = false)
    {
        if (sortedAscending.Count == 0)
            return new PercentileResult { N = 0, IncludesBackfilled = includesBackfilled };

        return new PercentileResult
        {
            N = sortedAscending.Count,
            P50 = Quantile(sortedAscending, 0.50),
            P85 = Quantile(sortedAscending, 0.85),
            P95 = Quantile(sortedAscending, 0.95),
            IncludesBackfilled = includesBackfilled
        };
    }

    public static PercentileResult FromValues(IEnumerable<double> values, bool includesBackfilled = false)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return FromSorted(sorted, includesBackfilled);
    }

    /// <summary>
    /// Nearest-rank percentile (inclusive). For n samples, rank = ceil(p * n), clamped to [1, n].
    /// Hand-checked: [10,20,30,40,50] → p50=30, p85=50, p95=50.
    /// </summary>
    public static double Quantile(IReadOnlyList<double> sortedAscending, double p)
    {
        if (sortedAscending.Count == 0)
            throw new ArgumentException("Empty sample", nameof(sortedAscending));
        if (p <= 0) return sortedAscending[0];
        if (p >= 1) return sortedAscending[^1];

        var rank = (int)Math.Ceiling(p * sortedAscending.Count);
        if (rank < 1) rank = 1;
        if (rank > sortedAscending.Count) rank = sortedAscending.Count;
        return sortedAscending[rank - 1];
    }
}

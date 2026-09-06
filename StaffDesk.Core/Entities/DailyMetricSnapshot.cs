namespace StaffDesk.Core.Entities;

/// <summary>
/// AN-3/AN-4: immutable daily rollup per department. Corrections write a new Version, never overwrite.
/// </summary>
public class DailyMetricSnapshot
{
    public long Id { get; set; }

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    /// <summary>UTC calendar date the snapshot describes.</summary>
    public DateOnly Date { get; set; }

    /// <summary>1-based; higher version supersedes older for "latest" reads; all versions retained.</summary>
    public int Version { get; set; } = 1;

    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    // --- Counts (AN-3) ---
    public int WipOpen { get; set; }
    public int WipInProgress { get; set; }
    public int WipBlocked { get; set; }
    public int WipInReview { get; set; }
    public int Throughput { get; set; }
    public int Arrivals { get; set; }
    public int BacklogSize { get; set; }
    public int BreachCount { get; set; }
    public int AtRiskCount { get; set; }

    // --- Duration percentiles for tasks completed that day (working minutes, CP-3) ---
    public int CompletedSampleN { get; set; }
    public double? LeadTimeP50 { get; set; }
    public double? LeadTimeP85 { get; set; }
    public double? LeadTimeP95 { get; set; }
    public double? CycleTimeP50 { get; set; }
    public double? CycleTimeP85 { get; set; }
    public double? CycleTimeP95 { get; set; }

    /// <summary>True if any sample used WC-26 backfilled intervals.</summary>
    public bool IncludesBackfilledIntervals { get; set; }

    /// <summary>JSON blob for optional extras (time-in-status breakdown, etc.).</summary>
    public string? ExtraJson { get; set; }
}

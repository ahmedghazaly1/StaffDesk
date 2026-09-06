using System.Text.Json;
using System.Text.Json.Serialization;

namespace StaffDesk.Core.Models;

/// <summary>Stored in DailyMetricSnapshot.ExtraJson for AN-8 range aggregation without task reloads.</summary>
public sealed class SnapshotExtra
{
    public List<double> LeadWorking { get; set; } = new();
    public List<double> LeadElapsed { get; set; } = new();
    public List<double> CycleWorking { get; set; } = new();
    public List<double> CycleElapsed { get; set; } = new();
    public List<double> ReactionWorking { get; set; } = new();
    public List<double> ReactionElapsed { get; set; } = new();
    public List<double> TriageWorking { get; set; } = new();
    public List<double> TriageElapsed { get; set; } = new();
    public List<double> FlowEfficiencyWorking { get; set; } = new();
    public List<double> FlowEfficiencyElapsed { get; set; } = new();
    public List<double> BlockedWorking { get; set; } = new();
    public List<double> BlockedElapsed { get; set; } = new();
    public List<double> EstimateAccuracyRatio { get; set; } = new();

    public Dictionary<string, List<double>> TimeInStatusWorking { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<double>> TimeInStatusElapsed { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int CompletedWithRework { get; set; }
    public int CompletedWithReopen { get; set; }
    public int FirstPassYieldNumerator { get; set; }
    public int ReworkTransitionCount { get; set; }
    public int ReopenTransitionCount { get; set; }
    public Dictionary<string, int> ReworkByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int UnestimatedCompletions { get; set; }
    public int UnestimatedActive { get; set; }
    public int WipEstimatedMinutes { get; set; }
    public int WipUnestimatedCount { get; set; }

    public int OnTimeNumerator { get; set; }
    public int OnTimeDenominator { get; set; }
    public int SlaMetNumerator { get; set; }
    public int SlaPolicyDenominator { get; set; }

    public int UntriagedRequests { get; set; }
    public int TriageDecidedCount { get; set; }

    public int Aging0to1 { get; set; }
    public int Aging1to3 { get; set; }
    public int Aging3to7 { get; set; }
    public int Aging7to14 { get; set; }
    public int Aging14Plus { get; set; }

    public long CapacityWorkingMinutes { get; set; }
    public long CommittedLoadMinutes { get; set; }
    public double? UtilisationPct { get; set; }

    public int OpenAtStart { get; set; }
    public int CarryOverCount { get; set; }

    public int NoEstimateActive { get; set; }
    public int StaleOpenIntervals { get; set; }
    public int BackfilledIntervalCount { get; set; }
    public int CompletedWithoutInProgress { get; set; }

    /// <summary>Per-assignee day aggregates for My Work analytics (AN-8 safe).</summary>
    public Dictionary<string, EmployeeDayMetrics> ByEmployee { get; set; } = new();

    public static SnapshotExtra Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SnapshotExtra();
        try
        {
            return JsonSerializer.Deserialize<SnapshotExtra>(json, JsonOpts) ?? new SnapshotExtra();
        }
        catch
        {
            return new SnapshotExtra();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}

public sealed class EmployeeDayMetrics
{
    public int Throughput { get; set; }
    public int ReworkTransitions { get; set; }
    public int ReopenTransitions { get; set; }
    public int CompletedWithRework { get; set; }
    public int CompletedWithReopen { get; set; }
}

public sealed class AnalyticsRebuildPayload
{
    public string? From { get; set; }
    public string? To { get; set; }
    public int? DepartmentId { get; set; }
}

public sealed class AnalyticsRollupPayload
{
    public string? Date { get; set; }
    public int? DepartmentId { get; set; }
}

public sealed class AnalyticsExportPayload
{
    public long ExportId { get; set; }
    public string Report { get; set; } = "throughput";
    public int DepartmentId { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? Basis { get; set; }
    public string? Granularity { get; set; }
    public string? Mode { get; set; }
    public int? OtherDepartmentId { get; set; }
    public double OverheadPercent { get; set; } = 20;
    public int ActorEmployeeId { get; set; }
    public string ActorRole { get; set; } = "Member";
}

public sealed class AnalyticsExportRequest
{
    public string Report { get; set; } = "throughput";
    public int DepartmentId { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? Basis { get; set; }
    public string? Granularity { get; set; }
    public string? Mode { get; set; }
    public int? OtherDepartmentId { get; set; }
    public double OverheadPercent { get; set; } = 20;
}

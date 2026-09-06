using System.Globalization;
using System.Text;
using System.Text.Json;
using StaffDesk.Core.Constants;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;

namespace StaffDesk.Core.Services;

/// <summary>
/// Part D — shared analytics (AN-1). Rollup writes immutable DailyMetricSnapshot rows (AN-3/4);
/// GET endpoints read snapshots only (AN-8).
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private const int MaxRangeDays = 366;
    private const int StaleOpenIntervalDays = 14;

    private readonly IAnalyticsRepository _repo;
    private readonly IWorkingCalendarService _calendar;
    private readonly IEmployeeRepository _employees;
    private readonly IDepartmentRepository _departments;
    private readonly ILeaveRepository _leave;
    private readonly IGovernanceRepository _governance;
    private readonly IJobRepository _jobs;
    private readonly IAuditService _audit;
    private readonly int _suppressionThreshold;

    public AnalyticsService(
        IAnalyticsRepository repo,
        IWorkingCalendarService calendar,
        IEmployeeRepository employees,
        IDepartmentRepository departments,
        ILeaveRepository leave,
        IGovernanceRepository governance,
        IJobRepository jobs,
        IAuditService audit,
        int suppressionThreshold = InsufficientData.DefaultThreshold)
    {
        _repo = repo;
        _calendar = calendar;
        _employees = employees;
        _departments = departments;
        _leave = leave;
        _governance = governance;
        _jobs = jobs;
        _audit = audit;
        _suppressionThreshold = suppressionThreshold;
    }

    public async Task<DailyMetricSnapshot> RollupDepartmentDayAsync(int departmentId, DateOnly date)
    {
        if (!await _departments.ExistsAsync(departmentId))
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Department not found", 404);

        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEndExclusive = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var asOf = dayEndExclusive.AddTicks(-1);
        var periodStart = dayStart;

        var tasks = await _repo.GetTasksForDepartmentAsync(departmentId);
        var taskIds = tasks.Select(t => t.Id).ToList();
        var intervals = taskIds.Count == 0
            ? Array.Empty<TaskStatusInterval>()
            : await _repo.GetIntervalsForTasksAsync(taskIds);
        var byTask = intervals.GroupBy(i => i.TaskId).ToDictionary(g => g.Key, g => g.ToList());
        var reworkEvents = taskIds.Count == 0
            ? Array.Empty<ReworkEvent>()
            : await _repo.GetReworkEventsForTasksAsync(taskIds);
        var assignments = taskIds.Count == 0
            ? Array.Empty<TaskActivity>()
            : await _repo.GetAssignmentActivitiesForTasksAsync(taskIds);
        var firstAssignment = assignments
            .GroupBy(a => a.TaskId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedAt).First().CreatedAt);

        var requests = await _repo.GetRequestsForDepartmentAsync(departmentId);

        var active = tasks.Where(t =>
            t.CreatedAt <= asOf
            && (t.CompletedAt == null || t.CompletedAt > asOf)
            && !IsTerminalAsOf(t, asOf)).ToList();

        var wipOpen = active.Count(t => StatusAsOf(t, byTask, asOf) == "OPEN");
        var wipInProgress = active.Count(t => StatusAsOf(t, byTask, asOf) == "IN_PROGRESS");
        var wipBlocked = active.Count(t => StatusAsOf(t, byTask, asOf) == "BLOCKED");
        var wipInReview = active.Count(t => StatusAsOf(t, byTask, asOf) == "IN_REVIEW");

        var untriaged = requests.Count(r =>
            r.SubmittedAt <= asOf
            && (r.Status == "SUBMITTED" || r.Status == "UNDER_TRIAGE")
            && (r.TriageDecidedAt == null || r.TriageDecidedAt > asOf));

        var backlog = wipOpen + untriaged;

        // Throughput: tasks that entered DONE that day (CompletedAt), regardless of later reopen.
        var completedToday = tasks.Where(t =>
            t.CompletedAt != null
            && t.CompletedAt >= dayStart
            && t.CompletedAt < dayEndExclusive).ToList();

        var arrivals = tasks.Count(t => t.CreatedAt >= dayStart && t.CreatedAt < dayEndExclusive);

        var breachCount = active.Count(t => string.Equals(t.BreachState, "BREACHED", StringComparison.OrdinalIgnoreCase));
        var atRiskCount = active.Count(t => string.Equals(t.BreachState, "AT_RISK", StringComparison.OrdinalIgnoreCase));

        var members = (await _employees.GetByDepartmentIdAsync(departmentId))
            .Where(e => e.IsActive && !e.IsErased).ToList();
        var cal = await _calendar.ResolveCalendarAsync(departmentId, asOf);
        var holidays = cal.Holidays?.ToList() ?? new List<CalendarHoliday>();
        var leave = await _leave.GetApprovedInRangeForEmployeesAsync(members.Select(m => m.Id), date, date);

        long capacity = 0;
        foreach (var m in members)
        {
            var memberLeave = leave.Where(l => l.EmployeeId == m.Id).ToList();
            capacity += CountAvailableWorkingMinutes(date, date, cal, holidays, memberLeave);
        }
        // default 20% overhead baked into utilisation snapshot
        var capacityNet = (long)(capacity * 0.80);
        long committed = 0;
        var unestimatedActive = 0;
        foreach (var t in active)
        {
            if (!t.EstimateMinutes.HasValue || t.EstimateMinutes.Value <= 0)
            {
                unestimatedActive++;
                continue;
            }
            committed += Math.Max(0, t.EstimateMinutes.Value - t.LoggedMinutes);
        }

        var extra = new SnapshotExtra
        {
            WipEstimatedMinutes = active.Where(t => t.EstimateMinutes.HasValue).Sum(t => t.EstimateMinutes!.Value),
            WipUnestimatedCount = unestimatedActive,
            UnestimatedActive = unestimatedActive,
            UntriagedRequests = untriaged,
            CapacityWorkingMinutes = capacityNet,
            CommittedLoadMinutes = committed,
            UtilisationPct = capacityNet > 0 ? Math.Round(100.0 * committed / capacityNet, 1) : null,
            NoEstimateActive = unestimatedActive
        };

        // Carry-over: open at start of day that are still open at EOD
        var openAtStart = tasks.Where(t =>
            t.CreatedAt < periodStart
            && (t.CompletedAt == null || t.CompletedAt >= periodStart)
            && !IsTerminalAsOf(t, periodStart.AddTicks(-1))).ToList();
        extra.OpenAtStart = openAtStart.Count;
        extra.CarryOverCount = openAtStart.Count(t => active.Any(a => a.Id == t.Id));

        // Aging WIP
        foreach (var t in active)
        {
            var taskIntervals = byTask.GetValueOrDefault(t.Id) ?? new List<TaskStatusInterval>();
            var firstIp = taskIntervals.Where(i => i.Status == "IN_PROGRESS").OrderBy(i => i.EnteredAt)
                .Select(i => (DateTime?)i.EnteredAt).FirstOrDefault();
            if (!firstIp.HasValue) continue;
            var ageDays = (asOf - firstIp.Value).TotalDays;
            if (ageDays < 1) extra.Aging0to1++;
            else if (ageDays < 3) extra.Aging1to3++;
            else if (ageDays < 7) extra.Aging3to7++;
            else if (ageDays < 14) extra.Aging7to14++;
            else extra.Aging14Plus++;
        }

        // Data quality: stale open intervals
        extra.StaleOpenIntervals = intervals.Count(i =>
            i.ExitedAt == null
            && (asOf - i.EnteredAt).TotalDays > StaleOpenIntervalDays);
        extra.BackfilledIntervalCount = intervals.Count(i => i.IsEstimated);

        // Triage decisions that day
        foreach (var r in requests.Where(r => r.TriageDecidedAt >= dayStart && r.TriageDecidedAt < dayEndExclusive))
        {
            extra.TriageDecidedCount++;
            extra.TriageWorking.Add(_calendar.GetWorkingMinutes(r.SubmittedAt, r.TriageDecidedAt!.Value, departmentId));
            extra.TriageElapsed.Add(Math.Max(0, (r.TriageDecidedAt.Value - r.SubmittedAt).TotalMinutes));
        }

        // Rework events that day
        var dayRework = reworkEvents.Where(e => e.OccurredAt >= dayStart && e.OccurredAt < dayEndExclusive).ToList();
        foreach (var e in dayRework)
        {
            if (e.IsReopen) extra.ReopenTransitionCount++;
            else
            {
                extra.ReworkTransitionCount++;
                var cat = string.IsNullOrWhiteSpace(e.Category) ? "uncategorized" : e.Category;
                extra.ReworkByCategory[cat] = extra.ReworkByCategory.GetValueOrDefault(cat) + 1;
            }

            var task = tasks.FirstOrDefault(t => t.Id == e.TaskId);
            if (task?.AssigneeId is int aid)
            {
                var key = aid.ToString();
                if (!extra.ByEmployee.TryGetValue(key, out var em))
                {
                    em = new EmployeeDayMetrics();
                    extra.ByEmployee[key] = em;
                }
                if (e.IsReopen) em.ReopenTransitions++;
                else em.ReworkTransitions++;
            }
        }

        var includesBackfilled = false;
        foreach (var task in completedToday)
        {
            var taskIntervals = byTask.GetValueOrDefault(task.Id) ?? new List<TaskStatusInterval>();
            if (taskIntervals.Any(i => i.IsEstimated)) includesBackfilled = true;

            var leadW = (double)_calendar.GetWorkingMinutes(task.CreatedAt, task.CompletedAt!.Value, departmentId);
            var leadE = Math.Max(0, (task.CompletedAt.Value - task.CreatedAt).TotalMinutes);
            extra.LeadWorking.Add(leadW);
            extra.LeadElapsed.Add(leadE);

            var firstIp = taskIntervals.Where(i => i.Status == "IN_PROGRESS").OrderBy(i => i.EnteredAt)
                .Select(i => (DateTime?)i.EnteredAt).FirstOrDefault();

            double inProgressW = 0, inProgressE = 0, blockedW = 0, blockedE = 0;
            foreach (var grp in taskIntervals.GroupBy(i => i.Status))
            {
                double wSum = 0, eSum = 0;
                foreach (var iv in grp)
                {
                    var exit = iv.ExitedAt ?? task.CompletedAt.Value;
                    if (exit <= iv.EnteredAt) continue;
                    wSum += _calendar.GetWorkingMinutes(iv.EnteredAt, exit, departmentId);
                    eSum += Math.Max(0, (exit - iv.EnteredAt).TotalMinutes);
                }
                AddToDict(extra.TimeInStatusWorking, grp.Key, wSum);
                AddToDict(extra.TimeInStatusElapsed, grp.Key, eSum);
                if (grp.Key == "IN_PROGRESS") { inProgressW = wSum; inProgressE = eSum; }
                if (grp.Key == "BLOCKED") { blockedW = wSum; blockedE = eSum; }
            }
            extra.BlockedWorking.Add(blockedW);
            extra.BlockedElapsed.Add(blockedE);

            if (firstIp.HasValue)
            {
                extra.CycleWorking.Add((double)_calendar.GetWorkingMinutes(firstIp.Value, task.CompletedAt.Value, departmentId));
                extra.CycleElapsed.Add(Math.Max(0, (task.CompletedAt.Value - firstIp.Value).TotalMinutes));
            }
            else
            {
                extra.CompletedWithoutInProgress++;
            }

            if (leadW > 0) extra.FlowEfficiencyWorking.Add(100.0 * inProgressW / leadW);
            if (leadE > 0) extra.FlowEfficiencyElapsed.Add(100.0 * inProgressE / leadE);

            if (firstAssignment.TryGetValue(task.Id, out var assignedAt))
            {
                extra.ReactionWorking.Add(_calendar.GetWorkingMinutes(task.CreatedAt, assignedAt, departmentId));
                extra.ReactionElapsed.Add(Math.Max(0, (assignedAt - task.CreatedAt).TotalMinutes));
            }

            var hadRework = task.ReworkCount > 0 || reworkEvents.Any(e => e.TaskId == task.Id && !e.IsReopen && e.OccurredAt <= task.CompletedAt);
            var hadReopen = task.ReopenCount > 0 || reworkEvents.Any(e => e.TaskId == task.Id && e.IsReopen && e.OccurredAt <= task.CompletedAt);
            if (hadRework) extra.CompletedWithRework++;
            if (hadReopen) extra.CompletedWithReopen++;
            if (!hadRework && !hadReopen) extra.FirstPassYieldNumerator++;

            if (!task.EstimateMinutes.HasValue || task.EstimateMinutes.Value <= 0)
                extra.UnestimatedCompletions++;
            else if (task.LoggedMinutes > 0)
                extra.EstimateAccuracyRatio.Add((double)task.LoggedMinutes / task.EstimateMinutes.Value);

            if (task.DueAt.HasValue)
            {
                extra.OnTimeDenominator++;
                if (task.CompletedAt <= task.DueAt) extra.OnTimeNumerator++;
            }

            if (task.ResolutionTargetAt.HasValue)
            {
                extra.SlaPolicyDenominator++;
                if (task.CompletedAt <= task.ResolutionTargetAt
                    || string.Equals(task.BreachState, "ON_TRACK", StringComparison.OrdinalIgnoreCase))
                    extra.SlaMetNumerator++;
                else if (!string.Equals(task.BreachState, "BREACHED", StringComparison.OrdinalIgnoreCase)
                         && task.CompletedAt <= task.ResolutionTargetAt)
                    extra.SlaMetNumerator++;
                else if (task.CompletedAt <= task.ResolutionTargetAt)
                    extra.SlaMetNumerator++;
            }

            if (task.AssigneeId is int aid)
            {
                var key = aid.ToString();
                if (!extra.ByEmployee.TryGetValue(key, out var em))
                {
                    em = new EmployeeDayMetrics();
                    extra.ByEmployee[key] = em;
                }
                em.Throughput++;
                if (hadRework) em.CompletedWithRework++;
                if (hadReopen) em.CompletedWithReopen++;
            }
        }

        // Fix SLA met: count completed with ResolutionTargetAt where CompletedAt <= target
        extra.SlaMetNumerator = 0;
        extra.SlaPolicyDenominator = 0;
        foreach (var task in completedToday.Where(t => t.ResolutionTargetAt.HasValue))
        {
            extra.SlaPolicyDenominator++;
            if (task.CompletedAt <= task.ResolutionTargetAt) extra.SlaMetNumerator++;
        }

        var leadPct = PercentileMath.FromValues(extra.LeadWorking, includesBackfilled);
        var cyclePct = PercentileMath.FromValues(extra.CycleWorking, includesBackfilled);

        var version = await _repo.GetLatestVersionAsync(departmentId, date) + 1;
        var snapshot = new DailyMetricSnapshot
        {
            DepartmentId = departmentId,
            Date = date,
            Version = version,
            ComputedAt = DateTime.UtcNow,
            WipOpen = wipOpen,
            WipInProgress = wipInProgress,
            WipBlocked = wipBlocked,
            WipInReview = wipInReview,
            Throughput = completedToday.Count,
            Arrivals = arrivals,
            BacklogSize = backlog,
            BreachCount = breachCount,
            AtRiskCount = atRiskCount,
            CompletedSampleN = completedToday.Count,
            LeadTimeP50 = leadPct.P50,
            LeadTimeP85 = leadPct.P85,
            LeadTimeP95 = leadPct.P95,
            CycleTimeP50 = cyclePct.P50,
            CycleTimeP85 = cyclePct.P85,
            CycleTimeP95 = cyclePct.P95,
            IncludesBackfilledIntervals = includesBackfilled,
            ExtraJson = extra.ToJson()
        };

        return await _repo.InsertSnapshotAsync(snapshot);
    }

    public async Task<int> RebuildAsync(DateOnly from, DateOnly to, int? departmentId = null)
    {
        ValidateRange(from, to);
        IReadOnlyList<int> deptIds;
        if (departmentId.HasValue)
        {
            if (!await _departments.ExistsAsync(departmentId.Value))
                throw new TaskDomainException(TaskErrorCodes.NotFound, "Department not found", 404);
            deptIds = new[] { departmentId.Value };
        }
        else
        {
            deptIds = await _repo.GetActiveDepartmentIdsAsync();
        }

        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            foreach (var id in deptIds)
            {
                await RollupDepartmentDayAsync(id, d);
                count++;
            }
        }
        return count;
    }

    public async Task<object> GetFlowAsync(int departmentId, DateOnly from, DateOnly to, string basis, string granularity, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        basis = NormalizeBasis(basis);
        granularity = NormalizeGranularity(granularity);
        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var byDate = snaps.ToDictionary(s => s.Date);

        var leadSamples = new List<double>();
        var cycleSamples = new List<double>();
        var reactionSamples = new List<double>();
        var triageSamples = new List<double>();
        var flowEffSamples = new List<double>();
        var blockedSamples = new List<double>();
        var tisSamples = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        var includesBackfilled = false;

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!byDate.TryGetValue(d, out var snap)) continue;
            var extra = SnapshotExtra.Parse(snap.ExtraJson);
            if (snap.IncludesBackfilledIntervals) includesBackfilled = true;
            leadSamples.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.LeadElapsed : extra.LeadWorking);
            cycleSamples.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.CycleElapsed : extra.CycleWorking);
            reactionSamples.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.ReactionElapsed : extra.ReactionWorking);
            triageSamples.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.TriageElapsed : extra.TriageWorking);
            flowEffSamples.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.FlowEfficiencyElapsed : extra.FlowEfficiencyWorking);
            blockedSamples.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.BlockedElapsed : extra.BlockedWorking);
            var tis = basis == MetricBasis.ElapsedMinutes ? extra.TimeInStatusElapsed : extra.TimeInStatusWorking;
            foreach (var kv in tis)
            {
                if (!tisSamples.TryGetValue(kv.Key, out var list))
                {
                    list = new List<double>();
                    tisSamples[kv.Key] = list;
                }
                list.AddRange(kv.Value);
            }
        }

        var daySeries = new List<object>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            byDate.TryGetValue(d, out var snap);
            var extra = snap == null ? new SnapshotExtra() : SnapshotExtra.Parse(snap.ExtraJson);
            var leadDay = basis == MetricBasis.ElapsedMinutes ? extra.LeadElapsed : extra.LeadWorking;
            var cycleDay = basis == MetricBasis.ElapsedMinutes ? extra.CycleElapsed : extra.CycleWorking;
            daySeries.Add(new
            {
                date = FormatDate(d),
                n = snap?.CompletedSampleN ?? 0,
                throughput = snap?.Throughput ?? 0,
                lead = ShapePercentiles(PercentileMath.FromValues(leadDay, snap?.IncludesBackfilledIntervals ?? false)),
                cycle = ShapePercentiles(PercentileMath.FromValues(cycleDay, snap?.IncludesBackfilledIntervals ?? false)),
                flowEfficiency = ShapePercentiles(PercentileMath.FromValues(
                    basis == MetricBasis.ElapsedMinutes ? extra.FlowEfficiencyElapsed : extra.FlowEfficiencyWorking,
                    snap?.IncludesBackfilledIntervals ?? false)),
                blocked = ShapePercentiles(PercentileMath.FromValues(
                    basis == MetricBasis.ElapsedMinutes ? extra.BlockedElapsed : extra.BlockedWorking,
                    snap?.IncludesBackfilledIntervals ?? false))
            });
        }

        return new
        {
            departmentId,
            from,
            to,
            basis,
            granularity,
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId),
            includesBackfilled,
            overall = new
            {
                lead = ShapePercentiles(PercentileMath.FromValues(leadSamples, includesBackfilled)),
                cycle = ShapePercentiles(PercentileMath.FromValues(cycleSamples, includesBackfilled)),
                reaction = ShapePercentiles(PercentileMath.FromValues(reactionSamples, includesBackfilled)),
                triageLatency = ShapePercentiles(PercentileMath.FromValues(triageSamples, includesBackfilled)),
                flowEfficiency = ShapePercentiles(PercentileMath.FromValues(flowEffSamples, includesBackfilled)),
                blocked = ShapePercentiles(PercentileMath.FromValues(blockedSamples, includesBackfilled)),
                timeInStatus = tisSamples.ToDictionary(
                    kv => kv.Key,
                    kv => ShapePercentiles(PercentileMath.FromValues(kv.Value, includesBackfilled)))
            },
            series = AggregateSeries(daySeries, granularity, from, to, AggregateFlowPoint)
        };
    }

    public async Task<object> GetThroughputAsync(int departmentId, DateOnly from, DateOnly to, string granularity, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        granularity = NormalizeGranularity(granularity);
        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var byDate = snaps.ToDictionary(s => s.Date);
        var daySeries = new List<object>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            byDate.TryGetValue(d, out var snap);
            daySeries.Add(new
            {
                date = FormatDate(d),
                throughput = snap?.Throughput ?? 0,
                arrivals = snap?.Arrivals ?? 0,
                backlog = snap?.BacklogSize ?? 0
            });
        }

        return new
        {
            departmentId,
            from,
            to,
            granularity,
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId),
            totals = new
            {
                throughput = snaps.Sum(s => s.Throughput),
                arrivals = snaps.Sum(s => s.Arrivals)
            },
            series = AggregateSeries(daySeries, granularity, from, to, AggregateCountPoint)
        };
    }

    public async Task<object> GetWipAsync(int departmentId, DateOnly from, DateOnly to, string granularity, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        granularity = NormalizeGranularity(granularity);
        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var byDate = snaps.ToDictionary(s => s.Date);
        var daySeries = new List<object>();
        int a01 = 0, a13 = 0, a37 = 0, a714 = 0, a14 = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            byDate.TryGetValue(d, out var snap);
            var extra = snap == null ? new SnapshotExtra() : SnapshotExtra.Parse(snap.ExtraJson);
            if (d == to || snap != null)
            {
                // latest aging from last available day in loop — accumulate end snapshot
            }
            daySeries.Add(new
            {
                date = FormatDate(d),
                open = snap?.WipOpen ?? 0,
                inProgress = snap?.WipInProgress ?? 0,
                blocked = snap?.WipBlocked ?? 0,
                inReview = snap?.WipInReview ?? 0,
                total = snap == null ? 0 : snap.WipOpen + snap.WipInProgress + snap.WipBlocked + snap.WipInReview
            });
            if (snap != null)
            {
                a01 = extra.Aging0to1; a13 = extra.Aging1to3; a37 = extra.Aging3to7;
                a714 = extra.Aging7to14; a14 = extra.Aging14Plus;
            }
        }

        return new
        {
            departmentId,
            from,
            to,
            granularity,
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId),
            agingBuckets = new
            {
                d0_1 = a01,
                d1_3 = a13,
                d3_7 = a37,
                d7_14 = a714,
                d14_plus = a14
            },
            series = AggregateSeries(daySeries, granularity, from, to, AggregateWipPoint)
        };
    }

    public async Task<object> GetSlaAsync(int departmentId, DateOnly from, DateOnly to, string granularity, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        granularity = NormalizeGranularity(granularity);
        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var byDate = snaps.ToDictionary(s => s.Date);
        var daySeries = new List<object>();
        var met = 0;
        var withPolicy = 0;
        foreach (var s in snaps)
        {
            var e = SnapshotExtra.Parse(s.ExtraJson);
            met += e.SlaMetNumerator;
            withPolicy += e.SlaPolicyDenominator;
        }

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            byDate.TryGetValue(d, out var snap);
            var wip = snap == null ? 0 : snap.WipOpen + snap.WipInProgress + snap.WipBlocked + snap.WipInReview;
            var breached = snap?.BreachCount ?? 0;
            var atRisk = snap?.AtRiskCount ?? 0;
            daySeries.Add(new
            {
                date = FormatDate(d),
                breached,
                atRisk,
                onTrack = Math.Max(0, wip - breached - atRisk)
            });
        }

        return new
        {
            departmentId,
            from,
            to,
            granularity,
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId),
            attainment = ShapeRatio(new RatioResult { Numerator = met, Denominator = withPolicy }),
            totals = new
            {
                breachedPeak = snaps.Count == 0 ? 0 : snaps.Max(s => s.BreachCount),
                atRiskPeak = snaps.Count == 0 ? 0 : snaps.Max(s => s.AtRiskCount)
            },
            series = AggregateSeries(daySeries, granularity, from, to, AggregateSlaPoint)
        };
    }

    public async Task<object> GetQualityAsync(int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var completed = snaps.Sum(s => s.Throughput);
        var withRework = 0;
        var withReopen = 0;
        var firstPass = 0;
        var reworkTransitions = 0;
        var reopenTransitions = 0;
        var byCat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var accuracy = new List<double>();
        var onTimeN = 0;
        var onTimeD = 0;

        foreach (var s in snaps)
        {
            var e = SnapshotExtra.Parse(s.ExtraJson);
            withRework += e.CompletedWithRework;
            withReopen += e.CompletedWithReopen;
            firstPass += e.FirstPassYieldNumerator;
            reworkTransitions += e.ReworkTransitionCount;
            reopenTransitions += e.ReopenTransitionCount;
            accuracy.AddRange(e.EstimateAccuracyRatio);
            onTimeN += e.OnTimeNumerator;
            onTimeD += e.OnTimeDenominator;
            foreach (var kv in e.ReworkByCategory)
                byCat[kv.Key] = byCat.GetValueOrDefault(kv.Key) + kv.Value;
        }

        return new
        {
            departmentId,
            from,
            to,
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId),
            completed,
            reworkRate = ShapeRatio(new RatioResult { Numerator = withRework, Denominator = completed }),
            reopenRate = ShapeRatio(new RatioResult { Numerator = withReopen, Denominator = completed }),
            firstPassYield = ShapeRatio(new RatioResult { Numerator = firstPass, Denominator = completed }),
            reworkTransitions,
            reopenTransitions,
            reworkByCategory = byCat,
            estimateAccuracy = ShapePercentiles(PercentileMath.FromValues(accuracy)),
            onTimeCompletion = ShapeRatio(new RatioResult { Numerator = onTimeN, Denominator = onTimeD })
        };
    }

    public async Task<object> GetWorkloadAsync(int departmentId, DateOnly from, DateOnly to, double overheadPercent, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        if (overheadPercent < 0 || overheadPercent > 90)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "overheadPercent must be between 0 and 90", 400);

        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var byDate = snaps.ToDictionary(s => s.Date);
        var series = new List<object>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            byDate.TryGetValue(d, out var snap);
            var extra = snap == null ? new SnapshotExtra() : SnapshotExtra.Parse(snap.ExtraJson);
            // Snapshots store utilisation at 20% overhead; rescale capacity if caller requests different overhead.
            var rawCapacity = extra.CapacityWorkingMinutes > 0
                ? (long)(extra.CapacityWorkingMinutes / 0.80)
                : 0L;
            var capacity = (long)(rawCapacity * (1.0 - overheadPercent / 100.0));
            var committed = extra.CommittedLoadMinutes;
            double? util = capacity > 0 ? Math.Round(100.0 * committed / capacity, 1) : null;
            series.Add(new
            {
                date = FormatDate(d),
                capacityWorkingMinutes = capacity,
                committedLoadMinutes = committed,
                utilisationPercent = util,
                unestimatedTaskCount = extra.WipUnestimatedCount,
                wipEstimatedMinutes = extra.WipEstimatedMinutes
            });
        }

        var last = snaps.LastOrDefault();
        var lastExtra = last == null ? new SnapshotExtra() : SnapshotExtra.Parse(last.ExtraJson);
        var openStart = snaps.Sum(s => SnapshotExtra.Parse(s.ExtraJson).OpenAtStart);
        // Carry-over: use first day's openAtStart and last day's carry relative — report from last snap extras average
        var carryN = snaps.Sum(s => SnapshotExtra.Parse(s.ExtraJson).CarryOverCount);
        var carryD = snaps.Sum(s => SnapshotExtra.Parse(s.ExtraJson).OpenAtStart);

        return new
        {
            departmentId,
            from,
            to,
            overheadPercent,
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId),
            latest = series.LastOrDefault(),
            carryOverRate = ShapeRatio(new RatioResult { Numerator = carryN, Denominator = carryD }),
            series
        };
    }

    public async Task<object> GetCompareAsync(int departmentId, DateOnly from, DateOnly to, string mode, int? otherDepartmentId, string basis, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        basis = NormalizeBasis(basis);
        mode = string.IsNullOrWhiteSpace(mode) ? "period" : mode.Trim().ToLowerInvariant();

        if (mode == "department")
        {
            if (!otherDepartmentId.HasValue)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "otherDepartmentId required for department compare", 400);
            await EnsureCanViewDeptAsync(otherDepartmentId.Value, actorEmployeeId, actorRole);
            var left = await SummarizeRangeAsync(departmentId, from, to, basis);
            var right = await SummarizeRangeAsync(otherDepartmentId.Value, from, to, basis);
            return new
            {
                mode,
                from,
                to,
                basis,
                dataAsOf = await _repo.GetNewestComputedAtAsync(),
                left,
                right,
                delta = DiffSummaries(left, right)
            };
        }

        var days = to.DayNumber - from.DayNumber + 1;
        var prevTo = from.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(days - 1));
        var current = await SummarizeRangeAsync(departmentId, from, to, basis);
        var previous = await SummarizeRangeAsync(departmentId, prevFrom, prevTo, basis);
        return new
        {
            mode = "period",
            departmentId,
            basis,
            current = new { from, to, metrics = current },
            previous = new { from = prevFrom, to = prevTo, metrics = previous },
            delta = DiffSummaries(current, previous),
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId)
        };
    }

    public async Task<object> GetDataQualityAsync(int departmentId, DateOnly from, DateOnly to, int actorEmployeeId, string actorRole)
    {
        await EnsureCanViewDeptAsync(departmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);
        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var expectedDays = to.DayNumber - from.DayNumber + 1;
        var daysWithSnapshot = snaps.Select(s => s.Date).Distinct().Count();
        var backfilledDays = snaps.Count(s => s.IncludesBackfilledIntervals);
        var noEstimate = 0;
        var stale = 0;
        var backfilledIntervals = 0;
        var withoutIp = 0;
        var completed = snaps.Sum(s => s.Throughput);
        var unestimatedCompletions = 0;
        foreach (var s in snaps)
        {
            var e = SnapshotExtra.Parse(s.ExtraJson);
            noEstimate = Math.Max(noEstimate, e.NoEstimateActive); // latest-ish; sum completions
            stale = Math.Max(stale, e.StaleOpenIntervals);
            backfilledIntervals += e.BackfilledIntervalCount;
            withoutIp += e.CompletedWithoutInProgress;
            unestimatedCompletions += e.UnestimatedCompletions;
        }

        return new
        {
            departmentId,
            from,
            to,
            dataAsOf = await _repo.GetNewestComputedAtAsync(departmentId),
            coverage = new
            {
                expectedDays,
                daysWithSnapshot,
                missingDays = Math.Max(0, expectedDays - daysWithSnapshot)
            },
            tasksWithNoEstimate = noEstimate,
            openIntervalsOlderThanDays = StaleOpenIntervalDays,
            staleOpenIntervals = stale,
            includesBackfilledDays = backfilledDays,
            backfilledIntervalCount = backfilledIntervals,
            completedWithoutInProgress = withoutIp,
            unestimatedCompletionRate = ShapeRatio(new RatioResult
            {
                Numerator = unestimatedCompletions,
                Denominator = completed
            }),
            suppressionThreshold = _suppressionThreshold
        };
    }

    public async Task<object> GetMeAsync(DateOnly from, DateOnly to, int actorEmployeeId, string actorRole)
    {
        ValidateRange(from, to);
        var actor = await _employees.GetByIdAsync(actorEmployeeId)
            ?? throw new TaskDomainException(TaskErrorCodes.NotFound, "Employee not found", 404);
        var deptId = actor.DepartmentId;

        await EnsureCanViewDeptAsync(deptId, actorEmployeeId, actorRole);
        var snaps = await _repo.GetLatestInRangeAsync(deptId, from, to);
        var key = actorEmployeeId.ToString();
        var series = new List<object>();
        var throughput = 0;
        var withRework = 0;
        var withReopen = 0;
        var reworkT = 0;
        var reopenT = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var snap = snaps.FirstOrDefault(s => s.Date == d);
            var em = snap == null
                ? new EmployeeDayMetrics()
                : SnapshotExtra.Parse(snap.ExtraJson).ByEmployee.GetValueOrDefault(key) ?? new EmployeeDayMetrics();
            throughput += em.Throughput;
            withRework += em.CompletedWithRework;
            withReopen += em.CompletedWithReopen;
            reworkT += em.ReworkTransitions;
            reopenT += em.ReopenTransitions;
            series.Add(new
            {
                date = FormatDate(d),
                throughput = em.Throughput,
                reworkTransitions = em.ReworkTransitions,
                reopenTransitions = em.ReopenTransitions
            });
        }

        return new
        {
            employeeId = actorEmployeeId,
            departmentId = deptId,
            from,
            to,
            dataAsOf = await _repo.GetNewestComputedAtAsync(deptId),
            caveat = "These figures measure the flow of work, not the worth of a person.",
            myThroughput = throughput,
            myReworkRate = ShapeRatio(new RatioResult { Numerator = withRework, Denominator = throughput }),
            myReopenRate = ShapeRatio(new RatioResult { Numerator = withReopen, Denominator = throughput }),
            reworkTransitions = reworkT,
            reopenTransitions = reopenT,
            series
        };
    }

    public async Task<object> GetOrganisationAsync(DateOnly from, DateOnly to, string basis, int actorEmployeeId, string actorRole)
    {
        if (actorRole is not (User.Roles.Admin or User.Roles.HrAdmin))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Organisation analytics is Admin/HR_ADMIN only", 403);
        ValidateRange(from, to);
        basis = NormalizeBasis(basis);
        var deptIds = await _repo.GetActiveDepartmentIdsAsync();
        var departments = new List<object>();
        foreach (var id in deptIds)
        {
            var summary = await SummarizeRangeAsync(id, from, to, basis);
            var snaps = await _repo.GetLatestInRangeAsync(id, from, to);
            var last = snaps.LastOrDefault();
            var extra = last == null ? new SnapshotExtra() : SnapshotExtra.Parse(last.ExtraJson);
            var met = snaps.Sum(s => SnapshotExtra.Parse(s.ExtraJson).SlaMetNumerator);
            var pol = snaps.Sum(s => SnapshotExtra.Parse(s.ExtraJson).SlaPolicyDenominator);
            departments.Add(new
            {
                departmentId = id,
                metrics = summary,
                backlog = last?.BacklogSize ?? 0,
                utilisationPercent = extra.UtilisationPct,
                unestimatedTaskCount = extra.WipUnestimatedCount,
                slaAttainment = ShapeRatio(new RatioResult { Numerator = met, Denominator = pol })
            });
        }

        return new
        {
            from,
            to,
            basis,
            dataAsOf = await _repo.GetNewestComputedAtAsync(),
            departments
        };
    }

    public async Task<DataExport> QueueExportAsync(AnalyticsExportRequest request, int actorEmployeeId, string actorRole)
    {
        if (!DateOnly.TryParse(request.From, out var from) || !DateOnly.TryParse(request.To, out var to))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "from and to (yyyy-MM-dd) required", 400);
        await EnsureCanViewDeptAsync(request.DepartmentId, actorEmployeeId, actorRole);
        ValidateRange(from, to);

        var filter = new AnalyticsExportPayload
        {
            Report = request.Report,
            DepartmentId = request.DepartmentId,
            From = from.ToString("yyyy-MM-dd"),
            To = to.ToString("yyyy-MM-dd"),
            Basis = request.Basis,
            Granularity = request.Granularity,
            Mode = request.Mode,
            OtherDepartmentId = request.OtherDepartmentId,
            OverheadPercent = request.OverheadPercent,
            ActorEmployeeId = actorEmployeeId,
            ActorRole = actorRole
        };

        var export = await _governance.CreateExportAsync(new DataExport
        {
            Type = DataExportTypes.Analytics,
            State = DataExportStates.Queued,
            RequestedByEmployeeId = actorEmployeeId,
            FilterJson = JsonSerializer.Serialize(filter),
            CreatedAt = DateTime.UtcNow
        });

        filter.ExportId = export.Id;
        var job = await _jobs.EnqueueAsync(JobTypes.AnalyticsExport, JsonSerializer.Serialize(filter));
        export.JobId = job.Id;
        await _governance.UpdateExportAsync(export);

        await _audit.LogAsync(
            "EXPORT_REQUESTED",
            actorEmployeeId,
            $"employee:{actorEmployeeId}",
            "SUCCESS",
            "DataExport",
            export.Id.ToString(),
            changes: new { export.Type, request.Report, request.DepartmentId, from, to });

        return (await _governance.GetExportByIdAsync(export.Id))!;
    }

    public async Task<DataExport?> GetExportAsync(long exportId, int actorEmployeeId, string actorRole)
    {
        var export = await _governance.GetExportByIdAsync(exportId);
        if (export == null) return null;
        if (export.Type != DataExportTypes.Analytics) return null;
        if (actorRole is not (User.Roles.Admin or User.Roles.HrAdmin)
            && export.RequestedByEmployeeId != actorEmployeeId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Not permitted to view this export", 403);
        return export;
    }

    public async Task ProcessAnalyticsExportAsync(long exportId)
    {
        var export = await _governance.GetExportByIdAsync(exportId)
            ?? throw new InvalidOperationException($"Export {exportId} not found");
        export.State = DataExportStates.Running;
        await _governance.UpdateExportAsync(export);

        try
        {
            var payload = JsonSerializer.Deserialize<AnalyticsExportPayload>(export.FilterJson)
                ?? throw new InvalidOperationException("Invalid export filter");
            var from = DateOnly.Parse(payload.From);
            var to = DateOnly.Parse(payload.To);
            var basis = payload.Basis ?? MetricBasis.WorkingMinutes;
            var granularity = payload.Granularity ?? "day";

            object data = payload.Report.ToLowerInvariant() switch
            {
                "flow" => await GetFlowAsync(payload.DepartmentId, from, to, basis, granularity, payload.ActorEmployeeId, payload.ActorRole),
                "wip" => await GetWipAsync(payload.DepartmentId, from, to, granularity, payload.ActorEmployeeId, payload.ActorRole),
                "sla" => await GetSlaAsync(payload.DepartmentId, from, to, granularity, payload.ActorEmployeeId, payload.ActorRole),
                "quality" => await GetQualityAsync(payload.DepartmentId, from, to, payload.ActorEmployeeId, payload.ActorRole),
                "workload" => await GetWorkloadAsync(payload.DepartmentId, from, to, payload.OverheadPercent, payload.ActorEmployeeId, payload.ActorRole),
                "compare" => await GetCompareAsync(payload.DepartmentId, from, to, payload.Mode ?? "period", payload.OtherDepartmentId, basis, payload.ActorEmployeeId, payload.ActorRole),
                "data-quality" => await GetDataQualityAsync(payload.DepartmentId, from, to, payload.ActorEmployeeId, payload.ActorRole),
                _ => await GetThroughputAsync(payload.DepartmentId, from, to, granularity, payload.ActorEmployeeId, payload.ActorRole)
            };

            var dir = Path.Combine(Path.GetTempPath(), "staffdesk-exports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"analytics-{exportId}-{payload.Report}.csv");
            var csv = ToCsv(data, payload.Report);
            await File.WriteAllTextAsync(path, csv, Encoding.UTF8);

            export.FilePath = path;
            export.RowCount = csv.Split('\n').Length - 1;
            export.State = DataExportStates.Succeeded;
            export.CompletedAt = DateTime.UtcNow;
            await _governance.UpdateExportAsync(export);
        }
        catch (Exception ex)
        {
            export.State = DataExportStates.Failed;
            export.Error = ex.Message;
            export.CompletedAt = DateTime.UtcNow;
            await _governance.UpdateExportAsync(export);
            throw;
        }
    }

    public PercentileResult ComputeLeadTimes(
        IEnumerable<(DateTime CreatedAt, DateTime CompletedAt, int DepartmentId)> tasks,
        string basis)
    {
        basis = NormalizeBasis(basis);
        var values = tasks.Select(t =>
            basis == MetricBasis.ElapsedMinutes
                ? Math.Max(0, (t.CompletedAt - t.CreatedAt).TotalMinutes)
                : (double)_calendar.GetWorkingMinutes(t.CreatedAt, t.CompletedAt, t.DepartmentId));
        return PercentileMath.FromValues(values);
    }

    public PercentileResult ComputeCycleTimes(
        IEnumerable<(DateTime? FirstInProgressAt, DateTime CompletedAt, int DepartmentId, bool IncludesBackfilled)> tasks,
        string basis)
    {
        basis = NormalizeBasis(basis);
        var list = tasks.Where(t => t.FirstInProgressAt.HasValue).ToList();
        var includes = list.Any(t => t.IncludesBackfilled);
        var values = list.Select(t =>
            basis == MetricBasis.ElapsedMinutes
                ? Math.Max(0, (t.CompletedAt - t.FirstInProgressAt!.Value).TotalMinutes)
                : (double)_calendar.GetWorkingMinutes(t.FirstInProgressAt!.Value, t.CompletedAt, t.DepartmentId));
        return PercentileMath.FromValues(values, includes);
    }

    // --- helpers ---

    private async Task<Dictionary<string, object?>> SummarizeRangeAsync(int departmentId, DateOnly from, DateOnly to, string basis)
    {
        var snaps = await _repo.GetLatestInRangeAsync(departmentId, from, to);
        var lead = new List<double>();
        var cycle = new List<double>();
        foreach (var s in snaps)
        {
            var extra = SnapshotExtra.Parse(s.ExtraJson);
            lead.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.LeadElapsed : extra.LeadWorking);
            cycle.AddRange(basis == MetricBasis.ElapsedMinutes ? extra.CycleElapsed : extra.CycleWorking);
        }
        return new Dictionary<string, object?>
        {
            ["departmentId"] = departmentId,
            ["throughput"] = snaps.Sum(s => s.Throughput),
            ["arrivals"] = snaps.Sum(s => s.Arrivals),
            ["avgWip"] = snaps.Count == 0
                ? null
                : snaps.Average(s => s.WipOpen + s.WipInProgress + s.WipBlocked + s.WipInReview),
            ["backlogEnd"] = snaps.LastOrDefault()?.BacklogSize ?? 0,
            ["lead"] = ShapePercentiles(PercentileMath.FromValues(lead)),
            ["cycle"] = ShapePercentiles(PercentileMath.FromValues(cycle))
        };
    }

    private static object DiffSummaries(Dictionary<string, object?> left, Dictionary<string, object?> right)
    {
        double L(string k) => left.TryGetValue(k, out var v) && v is IConvertible c ? Convert.ToDouble(c) : 0;
        double R(string k) => right.TryGetValue(k, out var v) && v is IConvertible c ? Convert.ToDouble(c) : 0;
        object Delta(string k)
        {
            var a = L(k);
            var b = R(k);
            var abs = a - b;
            double? rel = b == 0 ? (a == 0 ? 0 : null) : Math.Round(100.0 * abs / b, 1);
            return new { absolute = abs, relativePercent = rel };
        }
        return new
        {
            throughput = Delta("throughput"),
            arrivals = Delta("arrivals"),
            avgWip = Delta("avgWip"),
            backlogEnd = Delta("backlogEnd")
        };
    }

    private object ShapePercentiles(PercentileResult r)
    {
        if (r.N < _suppressionThreshold)
        {
            return new
            {
                n = r.N,
                status = InsufficientData.Marker,
                p50 = (double?)null,
                p85 = (double?)null,
                p95 = (double?)null,
                includesBackfilled = r.IncludesBackfilled
            };
        }

        return new
        {
            n = r.N,
            status = "ok",
            p50 = r.P50,
            p85 = r.P85,
            p95 = r.P95,
            includesBackfilled = r.IncludesBackfilled
        };
    }

    private static object ShapeRatio(RatioResult r)
    {
        if (r.Denominator < InsufficientData.DefaultThreshold)
        {
            return new
            {
                numerator = r.Numerator,
                denominator = r.Denominator,
                value = (double?)null,
                status = InsufficientData.Marker
            };
        }

        return new
        {
            numerator = r.Numerator,
            denominator = r.Denominator,
            value = r.Value,
            status = "ok"
        };
    }

    private static string NormalizeBasis(string? basis) =>
        string.Equals(basis, MetricBasis.ElapsedMinutes, StringComparison.OrdinalIgnoreCase)
            ? MetricBasis.ElapsedMinutes
            : MetricBasis.WorkingMinutes;

    private static string NormalizeGranularity(string? g)
    {
        if (string.Equals(g, "week", StringComparison.OrdinalIgnoreCase)) return "week";
        if (string.Equals(g, "month", StringComparison.OrdinalIgnoreCase)) return "month";
        return "day";
    }

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "to must be >= from", 400);
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, $"Range cannot exceed {MaxRangeDays} days", 400);
    }

    private static void AddToDict(Dictionary<string, List<double>> dict, string key, double value)
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = new List<double>();
            dict[key] = list;
        }
        list.Add(value);
    }

    private static bool IsTerminalAsOf(WorkTask t, DateTime asOf)
    {
        if (t.Status is "DONE" or "CANCELLED")
        {
            if (t.CompletedAt.HasValue && t.CompletedAt.Value > asOf) return false;
            // CANCELLED may lack CompletedAt — treat current terminal status as terminal if CreatedAt <= asOf
            if (!t.CompletedAt.HasValue && t.Status == "CANCELLED") return true;
            return t.CompletedAt.HasValue && t.CompletedAt.Value <= asOf;
        }
        return false;
    }

    private static string StatusAsOf(WorkTask task, Dictionary<int, List<TaskStatusInterval>> byTask, DateTime asOf)
    {
        if (!byTask.TryGetValue(task.Id, out var intervals) || intervals.Count == 0)
            return task.Status;

        var hit = intervals
            .Where(i => i.EnteredAt <= asOf && (i.ExitedAt == null || i.ExitedAt > asOf))
            .OrderByDescending(i => i.EnteredAt)
            .FirstOrDefault();
        return hit?.Status ?? task.Status;
    }

    private static string FormatDate(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string BucketKey(DateOnly d, string granularity)
    {
        if (granularity == "month") return $"{d.Year:D4}-{d.Month:D2}";
        if (granularity == "week")
        {
            var dt = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var cal = CultureInfo.InvariantCulture.Calendar;
            var week = cal.GetWeekOfYear(dt, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            // ISO week-year: if Jan and week >= 52 → previous year; if Dec and week == 1 → next year
            var year = d.Year;
            if (d.Month == 1 && week >= 52) year--;
            if (d.Month == 12 && week == 1) year++;
            return $"{year:D4}-W{week:D2}";
        }
        return FormatDate(d);
    }

    private static List<object> AggregateSeries(
        List<object> daySeries,
        string granularity,
        DateOnly from,
        DateOnly to,
        Func<IEnumerable<dynamic>, string, object> aggregator)
    {
        if (granularity == "day") return daySeries;
        return daySeries
            .Select(s => (dynamic)s)
            .GroupBy(s => BucketKey(DateOnly.Parse((string)s.date), granularity))
            .Select(g => aggregator(g, g.Key))
            .Cast<object>()
            .ToList();
    }

    private static object AggregateCountPoint(IEnumerable<dynamic> days, string key) => new
    {
        period = key,
        throughput = days.Sum(x => (int)x.throughput),
        arrivals = days.Sum(x => (int)x.arrivals),
        backlog = days.Select(x => (int)x.backlog).DefaultIfEmpty(0).Last()
    };

    private static object AggregateWipPoint(IEnumerable<dynamic> days, string key)
    {
        var list = days.ToList();
        var last = list.Last();
        return new
        {
            period = key,
            open = (int)last.open,
            inProgress = (int)last.inProgress,
            blocked = (int)last.blocked,
            inReview = (int)last.inReview,
            total = (int)last.total
        };
    }

    private static object AggregateSlaPoint(IEnumerable<dynamic> days, string key) => new
    {
        period = key,
        breached = days.Max(x => (int)x.breached),
        atRisk = days.Max(x => (int)x.atRisk),
        onTrack = days.Select(x => (int)x.onTrack).DefaultIfEmpty(0).Last()
    };

    private static object AggregateFlowPoint(IEnumerable<dynamic> days, string key)
    {
        var list = days.ToList();
        return new
        {
            period = key,
            n = list.Sum(x => (int)x.n),
            throughput = list.Sum(x => (int)x.throughput),
            // keep last day's shaped percentiles as period indicator (overall still authoritative)
            lead = list.Last().lead,
            cycle = list.Last().cycle
        };
    }

    private static long CountAvailableWorkingMinutes(
        DateOnly from, DateOnly to, WorkCalendar cal, IReadOnlyList<CalendarHoliday> holidays,
        IReadOnlyList<LeaveRequest> leave)
    {
        long dayMinutes = (cal.WorkEndHour - cal.WorkStartHour) * 60L;
        long total = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!IsWorkingDay(d, cal, holidays)) continue;
            var dayLeave = leave.Where(l => l.StartDate <= d && l.EndDate >= d).ToList();
            if (dayLeave.Count == 0) { total += dayMinutes; continue; }
            if (dayLeave.Any(l => !l.IsPartialDay)) continue;
            total += dayMinutes / 2;
        }
        return total;
    }

    private static bool IsWorkingDay(DateOnly localDate, WorkCalendar calendar, IReadOnlyCollection<CalendarHoliday> holidays)
    {
        if (holidays.Any(h => h.Date == localDate || (h.RecursAnnually && h.Date.Month == localDate.Month && h.Date.Day == localDate.Day)))
            return false;
        var dow = localDate.DayOfWeek;
        var bit = dow switch
        {
            DayOfWeek.Monday => WorkDayFlags.Monday,
            DayOfWeek.Tuesday => WorkDayFlags.Tuesday,
            DayOfWeek.Wednesday => WorkDayFlags.Wednesday,
            DayOfWeek.Thursday => WorkDayFlags.Thursday,
            DayOfWeek.Friday => WorkDayFlags.Friday,
            DayOfWeek.Saturday => WorkDayFlags.Saturday,
            _ => WorkDayFlags.Sunday
        };
        return (calendar.WorkDaysMask & bit) != 0;
    }

    private static string ToCsv(object data, string report)
    {
        var json = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        if (doc.RootElement.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
        {
            var rows = series.EnumerateArray().ToList();
            if (rows.Count == 0) return "date\n";
            var cols = rows[0].EnumerateObject().Select(p => p.Name).ToList();
            sb.AppendLine(string.Join(",", cols));
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",", cols.Select(c =>
                    EscapeCsv(row.TryGetProperty(c, out var v) ? v.ToString() : ""))));
            }
            return sb.ToString();
        }

        sb.AppendLine("key,value");
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name is "series" or "overall" or "departments") continue;
            sb.AppendLine($"{EscapeCsv(prop.Name)},{EscapeCsv(prop.Value.ToString())}");
        }
        sb.AppendLine($"report,{EscapeCsv(report)}");
        return sb.ToString();
    }

    private static string EscapeCsv(string? s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }

    private async Task EnsureCanViewDeptAsync(int departmentId, int actorEmployeeId, string actorRole)
    {
        if (!await _departments.ExistsAsync(departmentId))
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Department not found", 404);

        if (actorRole is User.Roles.Admin or User.Roles.HrAdmin) return;

        var actor = await _employees.GetByIdAsync(actorEmployeeId)
            ?? throw new TaskDomainException(TaskErrorCodes.Forbidden, "Not permitted to view this department", 403);

        if (actorRole == User.Roles.Manager)
        {
            if (actor.DepartmentId == departmentId) return;
            // managers may view departments of direct reports
            var reports = await _employees.GetDirectReportsAsync(actorEmployeeId);
            if (reports.Any(r => r.DepartmentId == departmentId)) return;
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Not permitted to view this department", 403);
        }

        if (actor.DepartmentId != departmentId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Not permitted to view this department", 403);
    }
}

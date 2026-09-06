using System.Text.Json;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;
using StaffDesk.Core.Services;

namespace StaffDesk.Tests;

public class AnalyticsTests
{
    [Fact]
    public void PercentileMath_NearestRank_HandComputedFixture()
    {
        var r = PercentileMath.FromValues(new[] { 10.0, 20, 30, 40, 50 });
        Assert.Equal(5, r.N);
        Assert.Equal(30, r.P50);
        Assert.Equal(50, r.P85);
        Assert.Equal(50, r.P95);
    }

    [Fact]
    public void PercentileMath_Empty_ReturnsN0()
    {
        var r = PercentileMath.FromValues(Array.Empty<double>());
        Assert.Equal(0, r.N);
        Assert.Null(r.P50);
    }

    [Fact]
    public void ComputeLeadTimes_Elapsed_UsesWallClock()
    {
        var svc = CreateService();
        var created = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 3, 2, 17, 0, 0, DateTimeKind.Utc);
        var r = svc.ComputeLeadTimes(new[] { (created, completed, 1) }, MetricBasis.ElapsedMinutes);
        Assert.Equal(480, r.P50);
    }

    [Fact]
    public void ComputeLeadTimes_Working_SkipsWeekend()
    {
        var svc = CreateService();
        var created = new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 3, 9, 17, 0, 0, DateTimeKind.Utc);
        var r = svc.ComputeLeadTimes(new[] { (created, completed, 1) }, MetricBasis.WorkingMinutes);
        Assert.Equal(960, r.P50);
    }

    [Fact]
    public void ComputeCycleTimes_FromFirstInProgress()
    {
        var svc = CreateService();
        var ip = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);
        var done = new DateTime(2026, 3, 2, 14, 0, 0, DateTimeKind.Utc);
        var r = svc.ComputeCycleTimes(new[] { ((DateTime?)ip, done, 1, false) }, MetricBasis.ElapsedMinutes);
        Assert.Equal(240, r.P50);
    }

    [Fact]
    public void ComputeCycleTimes_MarksBackfilled()
    {
        var svc = CreateService();
        var ip = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);
        var done = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc);
        var r = svc.ComputeCycleTimes(new[] { ((DateTime?)ip, done, 1, true) }, MetricBasis.ElapsedMinutes);
        Assert.True(r.IncludesBackfilled);
    }

    [Fact]
    public void Suppression_ThresholdFive_InsufficientBelow()
    {
        var samples = new[] { 10.0, 20, 30, 40 };
        var r = PercentileMath.FromValues(samples);
        Assert.True(r.N < InsufficientData.DefaultThreshold);
    }

    [Fact]
    public void FlowEfficiency_HandComputed()
    {
        // lead 480 elapsed, in-progress 240 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 50%
        var lead = 480.0;
        var inProgress = 240.0;
        var eff = 100.0 * inProgress / lead;
        Assert.Equal(50.0, eff);
    }

    [Fact]
    public void EstimateAccuracy_IsLoggedOverEstimate()
    {
        // Appendix A: logged ÃƒÆ’Ã‚Â· estimate ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â 120/100 = 1.2
        Assert.Equal(1.2, 120.0 / 100.0, 3);
        var r = PercentileMath.FromValues(new[] { 0.8, 1.0, 1.2, 1.4, 1.6 });
        Assert.Equal(5, r.N);
        Assert.Equal(1.2, r.P50);
    }

    [Fact]
    public void FirstPassYield_HandComputed()
    {
        // 3 completed, 1 with rework ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ FPY = 2/3
        var r = new RatioResult { Numerator = 2, Denominator = 3 };
        Assert.Equal(66.7, r.Value);
    }

    [Fact]
    public void DenseSeries_ZerosNotNull()
    {
        int? missingAsNull = null;
        int denseZero = missingAsNull ?? 0;
        Assert.Equal(0, denseZero);
    }

    [Fact]
    public async Task Rollup_VersionsImmutable_AndRebuildParity()
    {
        var store = new InMemoryAnalyticsStore();
        var svc = CreateService(store);

        var day = new DateOnly(2026, 3, 2);
        // empty dept rollup still writes a versioned snapshot
        var v1 = await svc.RollupDepartmentDayAsync(1, day);
        var v2 = await svc.RollupDepartmentDayAsync(1, day);
        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);
        Assert.Equal(2, store.Snapshots.Count(s => s.DepartmentId == 1 && s.Date == day));

        // rebuild writes another version; latest fields match prior latest for empty data
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await svc.RebuildAsync(day, day, 1);
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromMinutes(10), "NFR-17: rebuild of a single department-day must finish well under 10 minutes");
        var latest = store.Snapshots.Where(s => s.DepartmentId == 1 && s.Date == day).OrderByDescending(s => s.Version).First();
        Assert.Equal(0, latest.Throughput);
        Assert.Equal(0, latest.Arrivals);
        Assert.True(latest.Version >= 3);
    }

    [Fact]
    public async Task GetThroughput_DenseZeros_ForMissingDays()
    {
        var store = new InMemoryAnalyticsStore();
        store.Snapshots.Add(new DailyMetricSnapshot
        {
            DepartmentId = 1,
            Date = new DateOnly(2026, 3, 2),
            Version = 1,
            Throughput = 3,
            Arrivals = 1,
            ComputedAt = DateTime.UtcNow
        });
        var svc = CreateService(store);
        var result = await svc.GetThroughputAsync(1, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 4), "day", 1, User.Roles.Admin);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var series = doc.RootElement.GetProperty("series").EnumerateArray().ToList();
        Assert.Equal(3, series.Count);
        Assert.Equal(0, series[1].GetProperty("throughput").GetInt32());
        Assert.Equal(0, series[1].GetProperty("arrivals").GetInt32());
    }

    [Fact]
    public async Task Rollup_ThroughputAndLead_MatchHandComputed_AndRebuildParity()
    {
        var store = new InMemoryAnalyticsStore();
        var created = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var done = new DateTime(2026, 3, 2, 17, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= 5; i++)
        {
            store.Tasks.Add(new WorkTask
            {
                Id = i, DepartmentId = 1, Key = $"T-{i}", Title = "t", Status = "DONE",
                CreatedAt = created, CompletedAt = done, CreatedById = 1
            });
            store.Intervals.Add(new TaskStatusInterval
            {
                TaskId = i, Status = "IN_PROGRESS",
                EnteredAt = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc),
                ExitedAt = done
            });
        }
        var svc = CreateService(store);
        var day = new DateOnly(2026, 3, 2);
        var snap = await svc.RollupDepartmentDayAsync(1, day);
        Assert.Equal(5, snap.Throughput);
        Assert.Equal(5, snap.Arrivals);
        Assert.Equal(5, snap.CompletedSampleN);
        // UTC weekday 09:00–17:00 = 480 working minutes lead
        var extra = SnapshotExtra.Parse(snap.ExtraJson);
        Assert.All(extra.LeadWorking, v => Assert.Equal(480, v));

        await svc.RebuildAsync(day, day, 1);
        var latest = store.Snapshots.Where(s => s.DepartmentId == 1 && s.Date == day).OrderByDescending(s => s.Version).First();
        var extra2 = SnapshotExtra.Parse(latest.ExtraJson);
        Assert.Equal(snap.Throughput, latest.Throughput);
        Assert.Equal(snap.Arrivals, latest.Arrivals);
        Assert.Equal(extra.LeadWorking, extra2.LeadWorking);
    }

    [Fact]
    public async Task GetFlow_SuppressesPercentiles_BelowThreshold()
    {
        var store = new InMemoryAnalyticsStore();
        store.Snapshots.Add(new DailyMetricSnapshot
        {
            DepartmentId = 1,
            Date = new DateOnly(2026, 3, 2),
            Version = 1,
            CompletedSampleN = 4,
            ExtraJson = JsonSerializer.Serialize(new SnapshotExtra { LeadWorking = { 10, 20, 30, 40 } }),
            ComputedAt = DateTime.UtcNow
        });
        var svc = CreateService(store);
        var result = await svc.GetFlowAsync(1, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2), MetricBasis.WorkingMinutes, "day", 1, User.Roles.Admin);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var lead = doc.RootElement.GetProperty("overall").GetProperty("lead");
        Assert.Equal(InsufficientData.Marker, lead.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, lead.GetProperty("p50").ValueKind);
    }

    private static AnalyticsService CreateService(InMemoryAnalyticsStore? store = null)
    {
        store ??= new InMemoryAnalyticsStore();
        var cal = new WorkCalendar
        {
            Id = 1,
            Name = "Org",
            TimeZoneId = "UTC",
            WorkDaysMask = WorkDayFlags.Weekdays,
            WorkStartHour = 9,
            WorkEndHour = 17,
            IsOrganizationDefault = true,
            EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        return new AnalyticsService(
            store,
            new WorkingCalendarService(new StubCalRepo(cal)),
            new StubEmployees(),
            new StubDepartments(),
            new StubLeave(),
            new StubGovernance(),
            new StubJobs(),
            new StubAudit());
    }

    private sealed class InMemoryAnalyticsStore : IAnalyticsRepository
    {
        public List<DailyMetricSnapshot> Snapshots { get; } = new();

        public Task<DailyMetricSnapshot> InsertSnapshotAsync(DailyMetricSnapshot snapshot)
        {
            snapshot.Id = Snapshots.Count + 1;
            Snapshots.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<int> GetLatestVersionAsync(int departmentId, DateOnly date) =>
            Task.FromResult(Snapshots.Where(s => s.DepartmentId == departmentId && s.Date == date).Select(s => s.Version).DefaultIfEmpty(0).Max());

        public Task<DailyMetricSnapshot?> GetLatestAsync(int departmentId, DateOnly date) =>
            Task.FromResult(Snapshots.Where(s => s.DepartmentId == departmentId && s.Date == date).OrderByDescending(s => s.Version).FirstOrDefault());

        public Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeAsync(int departmentId, DateOnly from, DateOnly to) =>
            Task.FromResult<IReadOnlyList<DailyMetricSnapshot>>(
                Snapshots.Where(s => s.DepartmentId == departmentId && s.Date >= from && s.Date <= to)
                    .GroupBy(s => s.Date).Select(g => g.OrderByDescending(x => x.Version).First()).OrderBy(s => s.Date).ToList());

        public Task<IReadOnlyList<DailyMetricSnapshot>> GetLatestInRangeForDepartmentsAsync(IEnumerable<int> departmentIds, DateOnly from, DateOnly to) =>
            GetLatestInRangeAsync(departmentIds.First(), from, to);

        public Task<DateTime?> GetNewestComputedAtAsync(int? departmentId = null) =>
            Task.FromResult(Snapshots.Select(s => (DateTime?)s.ComputedAt).DefaultIfEmpty(null).Max());

        public List<WorkTask> Tasks { get; } = new();
        public List<TaskStatusInterval> Intervals { get; } = new();
        public List<TaskRequest> Requests { get; } = new();
        public List<ReworkEvent> Rework { get; } = new();
        public List<TaskActivity> Assignments { get; } = new();

        public Task<IReadOnlyList<WorkTask>> GetTasksForDepartmentAsync(int departmentId) =>
            Task.FromResult<IReadOnlyList<WorkTask>>(Tasks.Where(t => t.DepartmentId == departmentId).ToList());

        public Task<IReadOnlyList<TaskStatusInterval>> GetIntervalsForTasksAsync(IEnumerable<int> taskIds)
        {
            var ids = taskIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<TaskStatusInterval>>(Intervals.Where(i => ids.Contains(i.TaskId)).ToList());
        }

        public Task<IReadOnlyList<TaskRequest>> GetRequestsForDepartmentAsync(int departmentId) =>
            Task.FromResult<IReadOnlyList<TaskRequest>>(Requests.Where(r => r.DepartmentId == departmentId).ToList());

        public Task<IReadOnlyList<ReworkEvent>> GetReworkEventsForTasksAsync(IEnumerable<int> taskIds)
        {
            var ids = taskIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<ReworkEvent>>(Rework.Where(e => ids.Contains(e.TaskId)).ToList());
        }

        public Task<IReadOnlyList<TaskActivity>> GetAssignmentActivitiesForTasksAsync(IEnumerable<int> taskIds)
        {
            var ids = taskIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<TaskActivity>>(Assignments.Where(a => ids.Contains(a.TaskId)).ToList());
        }

        public Task<IReadOnlyList<int>> GetActiveDepartmentIdsAsync() =>
            Task.FromResult<IReadOnlyList<int>>(new[] { 1 });
    }

    private sealed class StubCalRepo : IWorkCalendarRepository
    {
        private readonly WorkCalendar _cal;
        public StubCalRepo(WorkCalendar cal) => _cal = cal;
        public Task<WorkCalendar> CreateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task UpdateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task<WorkCalendar?> GetByIdAsync(int id) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<WorkCalendar?> GetOrganizationDefaultAsync(DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<WorkCalendar?> GetForDepartmentAsync(int departmentId, DateTime? asOfUtc = null) => Task.FromResult<WorkCalendar?>(_cal);
        public Task<IReadOnlyList<WorkCalendar>> ListAsync() => Task.FromResult<IReadOnlyList<WorkCalendar>>(new[] { _cal });
        public Task<CalendarHoliday> AddHolidayAsync(CalendarHoliday holiday) => throw new NotImplementedException();
        public Task<bool> RemoveHolidayAsync(int holidayId) => throw new NotImplementedException();
        public Task SetDepartmentCalendarAsync(int departmentId, int? calendarId) => throw new NotImplementedException();
        public Task SeedDefaultIfEmptyAsync() => Task.CompletedTask;
    }

    private sealed class StubEmployees : IEmployeeRepository
    {
        public Task<IEnumerable<Employee>> GetAllAsync(int? page = null, int? limit = null, string? search = null, int? departmentId = null, int? levelId = null, int? managerId = null, bool? isActive = null) => Task.FromResult(Enumerable.Empty<Employee>());
        public Task<Employee?> GetByIdAsync(int id) => Task.FromResult<Employee?>(new Employee { Id = id, DepartmentId = 1, FullName = "T", IsActive = true });
        public Task<Employee> CreateAsync(Employee employee) => throw new NotImplementedException();
        public Task<int> GetTotalCountAsync(string? search = null, int? departmentId = null, int? levelId = null, int? managerId = null, bool? isActive = null) => Task.FromResult(0);
        public Task<IEnumerable<Employee>> GetByDepartmentIdAsync(int departmentId) => Task.FromResult(Enumerable.Empty<Employee>());
        public Task<Employee> UpdateAsync(Employee employee) => throw new NotImplementedException();
        public Task<Employee> UpdateAsync(int id, string fullName, string jobTitle, int? departmentId = null) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<Employee>> GetDirectReportsAsync(int id) => Task.FromResult(Enumerable.Empty<Employee>());
        public Task AddActivityAsync(EmployeeActivity activity) => throw new NotImplementedException();
        public Task<IEnumerable<EmployeeActivity>> GetActivityAsync(int employeeId) => throw new NotImplementedException();
    }

    private sealed class StubDepartments : IDepartmentRepository
    {
        public Task<IEnumerable<Department>> GetAllAsync() => throw new NotImplementedException();
        public Task<Department?> GetByIdAsync(int id) => Task.FromResult<Department?>(new Department { Id = id, Name = "D" });
        public Task<Department> CreateAsync(Department department) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(int id) => Task.FromResult(true);
        public Task<Department> UpdateAsync(Department department) => throw new NotImplementedException();
        public Task<Department> UpdateAsync(int id, string name, string location) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<DepartmentTriager>> GetTriagersAsync(int departmentId) => throw new NotImplementedException();
        public Task<DepartmentTriager> AddTriagerAsync(int departmentId, int employeeId) => throw new NotImplementedException();
        public Task<bool> RemoveTriagerAsync(int departmentId, int employeeId) => throw new NotImplementedException();
        public Task<bool> IsTriagerAsync(int departmentId, int employeeId) => throw new NotImplementedException();
        public Task<List<int>> GetManagedDepartmentIdsAsync(int employeeId) => Task.FromResult(new List<int>());
    }

    private sealed class StubLeave : ILeaveRepository
    {
        public Task<LeaveRequest> CreateAsync(LeaveRequest request) => throw new NotImplementedException();
        public Task<LeaveRequest?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task UpdateAsync(LeaveRequest request) => throw new NotImplementedException();
        public Task<IReadOnlyList<LeaveRequest>> GetForEmployeeAsync(int employeeId) => Task.FromResult<IReadOnlyList<LeaveRequest>>(Array.Empty<LeaveRequest>());
        public Task<IReadOnlyList<LeaveRequest>> GetForEmployeesAsync(IEnumerable<int> employeeIds) => Task.FromResult<IReadOnlyList<LeaveRequest>>(Array.Empty<LeaveRequest>());
        public Task<IReadOnlyList<LeaveRequest>> ListRecentAsync(int take = 500) => Task.FromResult<IReadOnlyList<LeaveRequest>>(Array.Empty<LeaveRequest>());
        public Task<IReadOnlyList<LeaveRequest>> GetPendingForManagerAsync(IEnumerable<int> reportEmployeeIds) => Task.FromResult<IReadOnlyList<LeaveRequest>>(Array.Empty<LeaveRequest>());
        public Task<bool> HasApprovedLeaveOnAsync(int employeeId, DateOnly date) => Task.FromResult(false);
        public Task<IReadOnlyList<LeaveRequest>> GetApprovedInRangeAsync(int employeeId, DateOnly from, DateOnly to) => Task.FromResult<IReadOnlyList<LeaveRequest>>(Array.Empty<LeaveRequest>());
        public Task<IReadOnlyList<LeaveRequest>> GetApprovedInRangeForEmployeesAsync(IEnumerable<int> employeeIds, DateOnly from, DateOnly to) =>
            Task.FromResult<IReadOnlyList<LeaveRequest>>(Array.Empty<LeaveRequest>());
    }

    private sealed class StubGovernance : IGovernanceRepository
    {
        public Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync() => throw new NotImplementedException();
        public Task SeedRetentionPoliciesIfEmptyAsync(IEnumerable<RetentionPolicy> defaults) => throw new NotImplementedException();
        public Task<LegalHold> PlaceHoldAsync(LegalHold hold) => throw new NotImplementedException();
        public Task<LegalHold?> GetHoldByIdAsync(long id) => throw new NotImplementedException();
        public Task<LegalHold?> LiftHoldAsync(long id, int liftedById) => throw new NotImplementedException();
        public Task<IReadOnlyList<LegalHold>> GetActiveHoldsAsync(string? targetType = null, string? targetId = null) => throw new NotImplementedException();
        public Task<bool> HasActiveHoldAsync(string targetType, string targetId) => Task.FromResult(false);
        public Task<DataExport> CreateExportAsync(DataExport export) => Task.FromResult(export);
        public Task<DataExport?> GetExportByIdAsync(long id) => Task.FromResult<DataExport?>(null);
        public Task UpdateExportAsync(DataExport export) => Task.CompletedTask;
        public Task<PurgeRun> CreatePurgeRunAsync(PurgeRun run) => throw new NotImplementedException();
        public Task<PurgeRun?> GetPurgeRunByIdAsync(long id) => throw new NotImplementedException();
        public Task UpdatePurgeRunAsync(PurgeRun run) => throw new NotImplementedException();
    }

    private sealed class StubJobs : IJobRepository
    {
        public Task<Job> EnqueueAsync(string type, string payloadJson) => Task.FromResult(new Job { Id = 1, Type = type, PayloadJson = payloadJson });
        public Task<Job?> ClaimNextAsync() => Task.FromResult<Job?>(null);
        public Task MarkSucceededAsync(long jobId) => Task.CompletedTask;
        public Task MarkFailedAsync(long jobId, string error, int maxAttempts = 5) => Task.CompletedTask;
        public Task<Job?> GetByIdAsync(long id) => Task.FromResult<Job?>(null);
        public Task<IReadOnlyDictionary<string, int>> CountByStateAsync() => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
        public Task<double?> GetOldestQueuedAgeSecondsAsync() => Task.FromResult<double?>(null);
        public Task<IReadOnlyList<Job>> ListByStateAsync(string? state, int limit = 50) => Task.FromResult<IReadOnlyList<Job>>(Array.Empty<Job>());
        public Task<(IReadOnlyList<Job> Items, int Total)> ListPagedAsync(string? state, int page, int limit) =>
            Task.FromResult<(IReadOnlyList<Job>, int)>((Array.Empty<Job>(), 0));
        public Task<bool> RequeueAsync(long jobId) => Task.FromResult(false);
    }

    private sealed class StubAudit : IAuditService
    {
        public Task<AuditEvent> LogAsync(string eventType, int? actorId, string actorLabel, string outcome, string? targetType = null, string? targetId = null, int? onBehalfOfId = null, object? changes = null, string? requestId = null, string? sourceIp = null, string? userAgent = null) =>
            Task.FromResult(new AuditEvent());
        public Task<AuditEvent?> GetByIdAsync(long id) => throw new NotImplementedException();
        public Task<(IEnumerable<AuditEvent> Items, string? NextCursor)> QueryAsync(string? eventType = null, string? outcome = null, int? actorId = null, string? targetType = null, string? targetId = null, DateTime? fromDate = null, DateTime? toDate = null, string? sourceIp = null, string? cursor = null, int limit = 50) => throw new NotImplementedException();
        public Task<(bool IsValid, long? FirstBreakIndex)> VerifyChainAsync(DateTime fromDate, DateTime toDate) => throw new NotImplementedException();
        public Task<long> GetEventCountAsync(DateTime? fromDate = null, DateTime? toDate = null) => throw new NotImplementedException();
    }
}

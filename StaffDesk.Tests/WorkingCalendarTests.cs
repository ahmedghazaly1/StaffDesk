using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Services;

namespace StaffDesk.Tests;

public class WorkingCalendarTests
{
    [Fact]
    public void GetWorkingMinutes_CountsWeekdayHours_SkipsWeekend()
    {
        var cal = WeekdayCalendar("UTC");
        var svc = new WorkingCalendarService(new StubCalendarRepo());

        // Monday 09:00 → Friday 17:00 UTC = 5 working days × 8h = 2400 minutes
        var from = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);  // Mon
        var to = new DateTime(2026, 3, 6, 17, 0, 0, DateTimeKind.Utc);    // Fri

        var minutes = svc.GetWorkingMinutes(from, to, cal, Array.Empty<CalendarHoliday>());
        Assert.Equal(5 * 8 * 60, minutes);
    }

    [Fact]
    public void GetWorkingMinutes_SkipsHoliday()
    {
        var cal = WeekdayCalendar("UTC");
        var holidays = new[]
        {
            new CalendarHoliday { Date = new DateOnly(2026, 3, 4), Name = "Midweek holiday" } // Wed
        };
        var svc = new WorkingCalendarService(new StubCalendarRepo());

        var from = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 6, 17, 0, 0, DateTimeKind.Utc);

        var minutes = svc.GetWorkingMinutes(from, to, cal, holidays);
        Assert.Equal(4 * 8 * 60, minutes);
    }

    [Fact]
    public void IsWorkingDay_RespectsWorkDaysMask()
    {
        var cal = WeekdayCalendar("UTC");
        var svc = new WorkingCalendarService(new StubCalendarRepo());
        Assert.True(svc.IsWorkingDay(new DateOnly(2026, 3, 2), cal, Array.Empty<CalendarHoliday>())); // Mon
        Assert.False(svc.IsWorkingDay(new DateOnly(2026, 3, 7), cal, Array.Empty<CalendarHoliday>())); // Sat
        Assert.False(svc.IsWorkingDay(new DateOnly(2026, 3, 8), cal, Array.Empty<CalendarHoliday>())); // Sun
    }

    [Fact]
    public void GetWorkingMinutes_HandlesSpringForwardDst_NamedCase()
    {
        // US Eastern spring-forward 2026-03-08 02:00 → 03:00
        var tzId = ResolveEasternTzId();
        if (tzId == null) return;

        var cal = WeekdayCalendar(tzId);
        var svc = new WorkingCalendarService(new StubCalendarRepo());

        // Monday after spring-forward: 09:00–17:00 local still 8 working hours
        var monStartUtc = new DateTime(2026, 3, 9, 13, 0, 0, DateTimeKind.Utc); // 09:00 EDT
        var monEndUtc = new DateTime(2026, 3, 9, 21, 0, 0, DateTimeKind.Utc);   // 17:00 EDT
        var minutes = svc.GetWorkingMinutes(monStartUtc, monEndUtc, cal, Array.Empty<CalendarHoliday>());
        Assert.Equal(8 * 60, minutes);
    }

    [Fact]
    public void GetWorkingMinutes_SpringForward_InsideWorkWindow_CountsTwoHoursNotThree()
    {
        var tzId = ResolveEasternTzId();
        if (tzId == null) return;

        var cal = new WorkCalendar
        {
            Name = "AllDays early",
            TimeZoneId = tzId,
            WorkDaysMask = WorkDayFlags.Sunday | WorkDayFlags.Weekdays | WorkDayFlags.Saturday,
            WorkStartHour = 1,
            WorkEndHour = 4,
            IsOrganizationDefault = true,
            EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var svc = new WorkingCalendarService(new StubCalendarRepo(cal));
        var from = new DateTime(2026, 3, 8, 6, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 8, 8, 0, 0, DateTimeKind.Utc);
        var minutes = svc.GetWorkingMinutes(from, to, cal, Array.Empty<CalendarHoliday>());
        Assert.Equal(120, minutes);
    }

    [Fact]
    public void GetWorkingMinutes_HandlesFallBackDst_NamedCase()
    {
        // US Eastern fall-back 2026-11-01 02:00 → 01:00
        var tzId = ResolveEasternTzId();
        if (tzId == null) return;

        var cal = WeekdayCalendar(tzId);
        var svc = new WorkingCalendarService(new StubCalendarRepo());

        // Monday 2026-11-02 after fall-back: still 8 local working hours
        var monStartUtc = new DateTime(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc); // 09:00 EST
        var monEndUtc = new DateTime(2026, 11, 2, 22, 0, 0, DateTimeKind.Utc);   // 17:00 EST
        var minutes = svc.GetWorkingMinutes(monStartUtc, monEndUtc, cal, Array.Empty<CalendarHoliday>());
        Assert.Equal(8 * 60, minutes);
    }

    [Fact]
    public void GetWorkingMinutes_AppliesMidPeriodCalendarChange_NamedCase()
    {
        // Week Mon–Fri: first half uses 8h/day calendar, second half uses 4h/day calendar.
        var early = WeekdayCalendar("UTC");
        early.Id = 1;
        early.Name = "Early";
        early.WorkStartHour = 9;
        early.WorkEndHour = 17;
        early.EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        early.EffectiveTo = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc); // ends Wed midnight UTC

        var late = WeekdayCalendar("UTC");
        late.Id = 2;
        late.Name = "Late";
        late.WorkStartHour = 9;
        late.WorkEndHour = 13; // 4h days
        late.EffectiveFrom = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);

        var repo = new StubCalendarRepo(early, late);
        var svc = new WorkingCalendarService(repo);

        var from = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc); // Mon
        var to = new DateTime(2026, 3, 6, 17, 0, 0, DateTimeKind.Utc);   // Fri end

        // Mon+Tue = 2×8h under early; Wed+Thu+Fri = 3×4h under late = 16+12 = 28h = 1680
        // Note: Wed 00:00 UTC starts late calendar; Mon 09:00–Tue end use early.
        // GetWorkingMinutes for each day: Mon 9-17, Tue 9-17 with early; Wed–Fri with late 9-13
        // But Friday to is 17:00 while late ends at 13 — only 4h counted Fri.
        var minutes = svc.GetWorkingMinutes(from, to, departmentId: 1);
        Assert.Equal((2 * 8 + 3 * 4) * 60, minutes);
    }

    private static WorkCalendar WeekdayCalendar(string tz) => new()
    {
        Name = "Test",
        TimeZoneId = tz,
        WorkDaysMask = WorkDayFlags.Weekdays,
        WorkStartHour = 9,
        WorkEndHour = 17,
        IsOrganizationDefault = true,
        EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static string? ResolveEasternTzId()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(id);
                return id;
            }
            catch { /* try next */ }
        }
        return null;
    }

    private sealed class StubCalendarRepo : IWorkCalendarRepository
    {
        private readonly List<WorkCalendar> _cals;

        public StubCalendarRepo(params WorkCalendar[] calendars)
        {
            _cals = calendars.ToList();
        }

        public Task<CalendarHoliday> AddHolidayAsync(CalendarHoliday holiday) => throw new NotImplementedException();
        public Task<WorkCalendar> CreateAsync(WorkCalendar calendar) => throw new NotImplementedException();
        public Task<WorkCalendar?> GetByIdAsync(int id) => Task.FromResult(_cals.FirstOrDefault(c => c.Id == id));

        public Task<WorkCalendar?> GetForDepartmentAsync(int departmentId, DateTime? asOfUtc = null)
        {
            var asOf = asOfUtc ?? DateTime.UtcNow;
            var match = _cals
                .Where(c => c.EffectiveFrom <= asOf && (c.EffectiveTo == null || c.EffectiveTo > asOf))
                .OrderByDescending(c => c.EffectiveFrom)
                .FirstOrDefault();
            return Task.FromResult(match);
        }

        public Task<WorkCalendar?> GetOrganizationDefaultAsync(DateTime? asOfUtc = null) =>
            GetForDepartmentAsync(0, asOfUtc);

        public Task<IReadOnlyList<WorkCalendar>> ListAsync() =>
            Task.FromResult<IReadOnlyList<WorkCalendar>>(_cals);

        public Task<bool> RemoveHolidayAsync(int holidayId) => Task.FromResult(false);
        public Task SeedDefaultIfEmptyAsync() => Task.CompletedTask;
        public Task SetDepartmentCalendarAsync(int departmentId, int? calendarId) => Task.CompletedTask;
        public Task UpdateAsync(WorkCalendar calendar) => Task.CompletedTask;
    }
}

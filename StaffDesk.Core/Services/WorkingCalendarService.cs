using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

/// <summary>
/// CP-3/CP-4: single shared working-minutes implementation used by SLA and duration metrics.
/// Instants are interpreted in the calendar's timezone so DST transitions are handled by TimeZoneInfo.
/// </summary>
public class WorkingCalendarService : IWorkingCalendarService
{
    private readonly IWorkCalendarRepository _calendars;

    public WorkingCalendarService(IWorkCalendarRepository calendars)
    {
        _calendars = calendars;
    }

    public async Task<WorkCalendar> ResolveCalendarAsync(int departmentId, DateTime? asOfUtc = null)
    {
        var cal = await _calendars.GetForDepartmentAsync(departmentId, asOfUtc)
                  ?? await _calendars.GetOrganizationDefaultAsync(asOfUtc);
        if (cal != null) return cal;

        // Safety net if seed hasn't run yet
        return new WorkCalendar
        {
            Name = "Fallback UTC Weekdays",
            TimeZoneId = "UTC",
            WorkDaysMask = WorkDayFlags.Weekdays,
            WorkStartHour = 9,
            WorkEndHour = 17,
            IsOrganizationDefault = true
        };
    }

    public long GetWorkingMinutes(DateTime from, DateTime to, int departmentId)
    {
        // CP-4: re-resolve the calendar at each local-day boundary so mid-period
        // EffectiveFrom / EffectiveTo changes are applied correctly.
        from = EnsureUtc(from);
        to = EnsureUtc(to);
        if (from >= to) return 0;

        long total = 0;
        var cursor = from;
        var guard = 0;
        while (cursor < to && guard++ < 20000)
        {
            var calendar = ResolveCalendarAsync(departmentId, cursor).GetAwaiter().GetResult();
            var holidays = calendar.Holidays?.ToList() ?? new List<CalendarHoliday>();
            var tz = ResolveTimeZone(calendar.TimeZoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(cursor, tz);
            var localDate = DateOnly.FromDateTime(local);
            var nextLocalMidnight = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            DateTime nextUtc;
            try
            {
                nextUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(nextLocalMidnight, DateTimeKind.Unspecified), tz);
            }
            catch (ArgumentException)
            {
                // Ambiguous/invalid local time around DST — step forward one UTC hour.
                nextUtc = cursor.AddHours(1);
            }

            if (nextUtc <= cursor)
                nextUtc = cursor.AddHours(1);

            var segmentEnd = nextUtc < to ? nextUtc : to;
            total += GetWorkingMinutes(cursor, segmentEnd, calendar, holidays);
            cursor = segmentEnd;
        }

        return total;
    }

    public long GetWorkingMinutes(DateTime from, DateTime to, WorkCalendar calendar, IReadOnlyCollection<CalendarHoliday> holidays)
    {
        from = EnsureUtc(from);
        to = EnsureUtc(to);
        if (from >= to) return 0;

        var tz = ResolveTimeZone(calendar.TimeZoneId);
        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(from, tz);
        var localTo = TimeZoneInfo.ConvertTimeFromUtc(to, tz);

        long total = 0;
        var cursor = localFrom;

        // Iterate in local time; convert segment ends back via timezone for correctness across DST.
        while (cursor < localTo)
        {
            var localDate = DateOnly.FromDateTime(cursor);
            if (!IsWorkingDay(localDate, calendar, holidays))
            {
                cursor = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
                continue;
            }

            var dayStart = localDate.ToDateTime(new TimeOnly(calendar.WorkStartHour, 0));
            var dayEnd = localDate.ToDateTime(new TimeOnly(Math.Min(calendar.WorkEndHour, 23), calendar.WorkEndHour >= 24 ? 0 : 0));
            if (calendar.WorkEndHour >= 24)
                dayEnd = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            else
                dayEnd = localDate.ToDateTime(new TimeOnly(calendar.WorkEndHour, 0));

            if (cursor < dayStart) cursor = dayStart;
            if (cursor >= dayEnd)
            {
                cursor = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
                continue;
            }

            var segmentEnd = localTo < dayEnd ? localTo : dayEnd;
            if (segmentEnd > cursor)
            {
                // Convert both ends to UTC so DST spring-forward / fall-back are reflected in elapsed time.
                var utcStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(cursor, DateTimeKind.Unspecified), tz);
                var utcEnd = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(segmentEnd, DateTimeKind.Unspecified), tz);
                if (utcEnd > utcStart)
                    total += (long)(utcEnd - utcStart).TotalMinutes;
            }

            cursor = segmentEnd;
            if (cursor >= dayEnd)
                cursor = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        }

        return total;
    }

    public DateTime AddWorkingMinutes(DateTime from, int minutes, int departmentId)
    {
        from = EnsureUtc(from);
        if (minutes <= 0) return from;

        var calendar = ResolveCalendarAsync(departmentId).GetAwaiter().GetResult();
        var holidays = calendar.Holidays?.ToList() ?? new List<CalendarHoliday>();
        var tz = ResolveTimeZone(calendar.TimeZoneId);
        var cursorLocal = TimeZoneInfo.ConvertTimeFromUtc(from, tz);
        var remaining = minutes;

        // Guard against infinite loops
        var guard = 0;
        while (remaining > 0 && guard++ < 20000)
        {
            var localDate = DateOnly.FromDateTime(cursorLocal);
            if (!IsWorkingDay(localDate, calendar, holidays))
            {
                cursorLocal = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
                continue;
            }

            var dayStart = localDate.ToDateTime(new TimeOnly(calendar.WorkStartHour, 0));
            var dayEnd = localDate.ToDateTime(new TimeOnly(calendar.WorkEndHour, 0));
            if (cursorLocal < dayStart) cursorLocal = dayStart;
            if (cursorLocal >= dayEnd)
            {
                cursorLocal = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
                continue;
            }

            var minutesLeftToday = (int)(dayEnd - cursorLocal).TotalMinutes;
            if (remaining <= minutesLeftToday)
            {
                cursorLocal = cursorLocal.AddMinutes(remaining);
                remaining = 0;
            }
            else
            {
                remaining -= minutesLeftToday;
                cursorLocal = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            }
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(cursorLocal, DateTimeKind.Unspecified), tz);
    }

    public bool IsWorkingTime(DateTime time, int departmentId)
    {
        time = EnsureUtc(time);
        var calendar = ResolveCalendarAsync(departmentId).GetAwaiter().GetResult();
        var holidays = calendar.Holidays?.ToList() ?? new List<CalendarHoliday>();
        var tz = ResolveTimeZone(calendar.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(time, tz);
        var date = DateOnly.FromDateTime(local);
        if (!IsWorkingDay(date, calendar, holidays)) return false;
        return local.Hour >= calendar.WorkStartHour && local.Hour < calendar.WorkEndHour;
    }

    public bool IsWorkingDay(DateOnly localDate, WorkCalendar calendar, IReadOnlyCollection<CalendarHoliday> holidays)
    {
        var flag = WorkDayFlags.FromDayOfWeek(localDate.DayOfWeek);
        if ((calendar.WorkDaysMask & flag) == 0) return false;

        foreach (var h in holidays)
        {
            if (h.RecursAnnually)
            {
                if (h.Date.Month == localDate.Month && h.Date.Day == localDate.Day)
                    return false;
            }
            else if (h.Date == localDate)
            {
                return false;
            }
        }

        return true;
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException)
        {
            // Windows may need different ids; try UTC fallback.
            try { return TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
            catch { return TimeZoneInfo.Utc; }
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTime EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return dateTime.ToUniversalTime();
    }
}

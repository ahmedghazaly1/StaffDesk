namespace StaffDesk.Core.Interfaces;

public interface IWorkingCalendarService
{
    // CP-3: convert any two instants into elapsed working minutes for a given department
    long GetWorkingMinutes(DateTime from, DateTime to, int departmentId);

    // Overload that uses a preloaded calendar (tests / hot paths)
    long GetWorkingMinutes(DateTime from, DateTime to, Entities.WorkCalendar calendar, IReadOnlyCollection<Entities.CalendarHoliday> holidays);

    DateTime AddWorkingMinutes(DateTime from, int minutes, int departmentId);

    bool IsWorkingTime(DateTime time, int departmentId);

    bool IsWorkingDay(DateOnly localDate, Entities.WorkCalendar calendar, IReadOnlyCollection<Entities.CalendarHoliday> holidays);

    Task<Entities.WorkCalendar> ResolveCalendarAsync(int departmentId, DateTime? asOfUtc = null);
}

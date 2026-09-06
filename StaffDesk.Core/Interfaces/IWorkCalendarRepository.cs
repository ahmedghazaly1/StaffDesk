using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IWorkCalendarRepository
{
    Task<WorkCalendar?> GetOrganizationDefaultAsync(DateTime? asOfUtc = null);
    Task<WorkCalendar?> GetForDepartmentAsync(int departmentId, DateTime? asOfUtc = null);
    Task<WorkCalendar?> GetByIdAsync(int id);
    Task<IReadOnlyList<WorkCalendar>> ListAsync();
    Task<WorkCalendar> CreateAsync(WorkCalendar calendar);
    Task UpdateAsync(WorkCalendar calendar);
    Task<CalendarHoliday> AddHolidayAsync(CalendarHoliday holiday);
    Task<bool> RemoveHolidayAsync(int holidayId);
    Task SetDepartmentCalendarAsync(int departmentId, int? calendarId);
    Task SeedDefaultIfEmptyAsync();
}

using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class WorkCalendarRepository : IWorkCalendarRepository
{
    private readonly AppDbContext _db;

    public WorkCalendarRepository(AppDbContext db) => _db = db;

    public async Task SeedDefaultIfEmptyAsync()
    {
        if (await _db.WorkCalendars.AnyAsync()) return;

        var cal = new WorkCalendar
        {
            Name = "Organization Default",
            TimeZoneId = "UTC",
            WorkDaysMask = WorkDayFlags.Weekdays,
            WorkStartHour = 9,
            WorkEndHour = 17,
            IsOrganizationDefault = true,
            EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _db.WorkCalendars.Add(cal);
        await _db.SaveChangesAsync();
    }

    public async Task<WorkCalendar?> GetOrganizationDefaultAsync(DateTime? asOfUtc = null)
    {
        var asOf = asOfUtc ?? DateTime.UtcNow;
        return await _db.WorkCalendars.AsNoTracking()
            .Include(c => c.Holidays)
            .Where(c => c.IsOrganizationDefault
                        && c.EffectiveFrom <= asOf
                        && (c.EffectiveTo == null || c.EffectiveTo > asOf))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync();
    }

    public async Task<WorkCalendar?> GetForDepartmentAsync(int departmentId, DateTime? asOfUtc = null)
    {
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var dept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == departmentId);
        if (dept?.WorkCalendarId != null)
        {
            var overrideCal = await _db.WorkCalendars.AsNoTracking()
                .Include(c => c.Holidays)
                .FirstOrDefaultAsync(c => c.Id == dept.WorkCalendarId
                                          && c.EffectiveFrom <= asOf
                                          && (c.EffectiveTo == null || c.EffectiveTo > asOf));
            if (overrideCal != null) return overrideCal;
        }

        return await GetOrganizationDefaultAsync(asOf);
    }

    public Task<WorkCalendar?> GetByIdAsync(int id) =>
        _db.WorkCalendars.Include(c => c.Holidays).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyList<WorkCalendar>> ListAsync() =>
        await _db.WorkCalendars.AsNoTracking().Include(c => c.Holidays).OrderBy(c => c.Name).ToListAsync();

    public async Task<WorkCalendar> CreateAsync(WorkCalendar calendar)
    {
        if (calendar.IsOrganizationDefault)
        {
            var existing = await _db.WorkCalendars.Where(c => c.IsOrganizationDefault).ToListAsync();
            foreach (var e in existing) e.IsOrganizationDefault = false;
        }

        _db.WorkCalendars.Add(calendar);
        await _db.SaveChangesAsync();
        return calendar;
    }

    public async Task UpdateAsync(WorkCalendar calendar)
    {
        _db.WorkCalendars.Update(calendar);
        await _db.SaveChangesAsync();
    }

    public async Task<CalendarHoliday> AddHolidayAsync(CalendarHoliday holiday)
    {
        _db.CalendarHolidays.Add(holiday);
        await _db.SaveChangesAsync();
        return holiday;
    }

    public async Task<bool> RemoveHolidayAsync(int holidayId)
    {
        var h = await _db.CalendarHolidays.FindAsync(holidayId);
        if (h == null) return false;
        _db.CalendarHolidays.Remove(h);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SetDepartmentCalendarAsync(int departmentId, int? calendarId)
    {
        var dept = await _db.Departments.FindAsync(departmentId)
            ?? throw new KeyNotFoundException("Department not found");
        if (calendarId.HasValue && !await _db.WorkCalendars.AnyAsync(c => c.Id == calendarId))
            throw new KeyNotFoundException("Calendar not found");
        dept.WorkCalendarId = calendarId;
        await _db.SaveChangesAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class TimesheetRepository : ITimesheetRepository
{
    private readonly AppDbContext _db;
    public TimesheetRepository(AppDbContext db) => _db = db;

    /// <summary>Normalizes any date to the first day of its month (period start).</summary>
    public static DateOnly MonthStartOf(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly MonthEndOf(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    public async Task<Timesheet> GetOrCreateOpenWeekAsync(int employeeId, DateOnly weekStart)
    {
        weekStart = MonthStartOf(weekStart);
        var existing = await _db.Timesheets
            .Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStart == weekStart);
        if (existing != null) return existing;

        var sheet = new Timesheet
        {
            EmployeeId = employeeId,
            WeekStart = weekStart,
            State = TimesheetStates.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Timesheets.Add(sheet);
        await _db.SaveChangesAsync();
        return sheet;
    }

    public Task<Timesheet?> GetByIdAsync(int id) =>
        _db.Timesheets.Include(t => t.Entries).Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task UpdateAsync(Timesheet timesheet)
    {
        timesheet.UpdatedAt = DateTime.UtcNow;
        _db.Timesheets.Update(timesheet);
        await _db.SaveChangesAsync();
    }

    public async Task AttachTimeEntryAsync(int timeEntryId, int timesheetId)
    {
        var entry = await _db.TaskTimeEntries.FindAsync(timeEntryId)
            ?? throw new KeyNotFoundException("Time entry not found");
        entry.TimesheetId = timesheetId;
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Timesheet>> GetForEmployeeAsync(int employeeId) =>
        await _db.Timesheets.AsNoTracking()
            .Include(t => t.Entries)
            .Where(t => t.EmployeeId == employeeId)
            .OrderByDescending(t => t.WeekStart)
            .ToListAsync();

    public async Task<IReadOnlyList<Timesheet>> GetSubmittedForReviewerAsync(IEnumerable<int> reportEmployeeIds)
    {
        var ids = reportEmployeeIds.ToList();
        return await _db.Timesheets.AsNoTracking()
            .Include(t => t.Employee)
            .Include(t => t.Entries)
            .Where(t => t.State == TimesheetStates.Submitted && ids.Contains(t.EmployeeId))
            .OrderBy(t => t.WeekStart)
            .ToListAsync();
    }
}

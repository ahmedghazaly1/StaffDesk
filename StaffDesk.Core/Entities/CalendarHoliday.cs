namespace StaffDesk.Core.Entities;

/// <summary>CP-2: holiday on a calendar; optional annual recurrence for fixed-date holidays.</summary>
public class CalendarHoliday
{
    public int Id { get; set; }
    public int CalendarId { get; set; }
    public WorkCalendar Calendar { get; set; } = null!;

    /// <summary>Calendar date (date-only meaning in the calendar's timezone).</summary>
    public DateOnly Date { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>If true, repeats every year on the same month/day.</summary>
    public bool RecursAnnually { get; set; }
}

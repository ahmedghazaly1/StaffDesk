namespace StaffDesk.Core.Entities;

/// <summary>CP-1: working days, hours, and timezone — org default or per-department override.</summary>
public class WorkCalendar
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>IANA timezone id, e.g. Africa/Cairo or UTC.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Bit flags: Sun=1, Mon=2, Tue=4, Wed=8, Thu=16, Fri=32, Sat=64. Default Mon–Fri = 62.</summary>
    public int WorkDaysMask { get; set; } = 62;

    /// <summary>Local start hour of the working day (0–23).</summary>
    public int WorkStartHour { get; set; } = 9;

    /// <summary>Local end hour of the working day (1–24), exclusive end of last hour block.</summary>
    public int WorkEndHour { get; set; } = 17;

    public bool IsOrganizationDefault { get; set; }

    public DateTime EffectiveFrom { get; set; } = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public DateTime? EffectiveTo { get; set; }

    public ICollection<CalendarHoliday> Holidays { get; set; } = new List<CalendarHoliday>();
}

public static class WorkDayFlags
{
    public const int Sunday = 1;
    public const int Monday = 2;
    public const int Tuesday = 4;
    public const int Wednesday = 8;
    public const int Thursday = 16;
    public const int Friday = 32;
    public const int Saturday = 64;
    public const int Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday;

    public static int FromDayOfWeek(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday => Sunday,
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        DayOfWeek.Saturday => Saturday,
        _ => 0
    };
}

namespace StaffDesk.Core.Entities;

public class RecurrenceRule
{
    public int Id { get; set; }

    public int TemplateId { get; set; }
    public TaskTemplate Template { get; set; } = null!;

    public string Frequency { get; set; } = "DAILY";  // DAILY, WEEKLY, MONTHLY

    // For weekly: comma-separated days (0=Sunday, 1=Monday, etc.)
    public string? DaysOfWeek { get; set; }  // e.g., "1,3,5" for Mon, Wed, Fri

    // For monthly: day of month (1-31) or "last" or "1st Monday", etc.
    public string? DayOfMonth { get; set; }  // e.g., "15" or "1st Monday"

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string Timezone { get; set; } = "UTC";

    public bool GenerateOnlyWhenPreviousComplete { get; set; } = false;

    public bool IsPaused { get; set; } = false;

    // RC-5: SKIP, NEXT_WORKING_DAY, PREVIOUS_WORKING_DAY
    public string NonWorkingDayPolicy { get; set; } = "NEXT_WORKING_DAY";

    public DateTime? LastGeneratedAt { get; set; }
    public DateTime? NextGenerationAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
namespace StaffDesk.Core.Entities;

public class RecurrenceOccurrence
{
    public int Id { get; set; }

    public int RuleId { get; set; }
    public RecurrenceRule Rule { get; set; } = null!;

    public int? TaskId { get; set; }
    public WorkTask? Task { get; set; }

    public DateTime OccurrenceDate { get; set; }

    public string State { get; set; } = "PENDING";  // PENDING, GENERATED, SKIPPED, COMPLETED

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
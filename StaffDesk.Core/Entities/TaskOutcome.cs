namespace StaffDesk.Core.Entities;

public class TaskOutcome
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;

    public string Outcome { get; set; } = string.Empty;  // DELIVERED, DELIVERED_PARTIAL, SUPERSEDED, NOT_REPRODUCIBLE, DUPLICATE, WONT_DO

    public string? Note { get; set; }

    public string? ExternalReference { get; set; }  // URL or reference
    public string? ExternalReferenceLabel { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
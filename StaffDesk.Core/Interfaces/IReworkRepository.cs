using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IReworkRepository
{
    // Create a rework event
    Task<ReworkEvent> CreateReworkEventAsync(ReworkEvent reworkEvent);

    // Get all rework events for a task
    Task<IEnumerable<ReworkEvent>> GetReworkEventsByTaskIdAsync(int taskId);

    // Get rework events with filters (for reporting)
    Task<IEnumerable<ReworkEvent>> GetReworkEventsAsync(
        int? departmentId = null,
        string? category = null,
        bool? isReopen = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? limit = null
    );

    // Get rework statistics for a department
    Task<ReworkStatistics> GetReworkStatisticsAsync(
        int departmentId,
        DateTime? fromDate = null,
        DateTime? toDate = null
    );
}

// DTO for rework statistics
public class ReworkStatistics
{
    public int TotalReworks { get; set; }
    public int TotalReopens { get; set; }
    public Dictionary<string, int> ReworksByCategory { get; set; } = new Dictionary<string, int>();
    public double FirstPassYield { get; set; } // (TotalTasks - Reworks - Reopens) / TotalTasks
    public int TotalTasksCompleted { get; set; }
}
using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IRecurrenceService
{
    // ============================================
    // Templates (RC-1)
    // ============================================
    Task<TaskTemplate> CreateTemplateAsync(
        string name,
        string? description,
        string titlePattern,
        string? defaultDescription,
        string defaultPriority,
        int? defaultAssigneeId,
        string? defaultAssignmentRule,
        int? defaultEstimateMinutes,
        List<string> defaultTags,
        List<string> defaultChecklistItems,
        List<string> defaultAcceptanceCriteria,
        int departmentId,
        int createdBy
    );

    Task<TaskTemplate?> GetTemplateByIdAsync(int id);
    Task<IEnumerable<TaskTemplate>> GetTemplatesByDepartmentAsync(int departmentId);
    Task<TaskTemplate> UpdateTemplateAsync(TaskTemplate template);
    Task<bool> DeleteTemplateAsync(int id);

    // ============================================
    // Recurrence Rules (RC-2, RC-3, RC-4, RC-5, RC-6)
    // ============================================
    Task<RecurrenceRule> CreateRuleAsync(
        int templateId,
        string frequency,
        string? daysOfWeek,
        string? dayOfMonth,
        DateTime startDate,
        DateTime? endDate,
        string timezone,
        bool generateOnlyWhenPreviousComplete,
        bool isPaused,
        string nonWorkingDayPolicy = "NEXT_WORKING_DAY"
    );

    Task<RecurrenceRule> GetRuleByIdAsync(int id);  // ADD THIS

    Task<RecurrenceRule> UpdateRuleAsync(RecurrenceRule rule);
    Task<bool> DeleteRuleAsync(int id);
    Task<bool> PauseRuleAsync(int id);
    Task<bool> ResumeRuleAsync(int id);

    // ============================================
    // Occurrence Generation (RC-3, RC-5)
    // ============================================
    Task<int> GenerateOccurrencesAsync(int actorId);
    Task<IEnumerable<RecurrenceOccurrence>> GetPendingOccurrencesAsync();
    Task<IEnumerable<RecurrenceOccurrence>> GetOccurrencesByRuleIdAsync(int ruleId);

    // ============================================
    // Calculate Next Occurrence
    // ============================================
    DateTime? CalculateNextOccurrence(RecurrenceRule rule, DateTime? fromDate = null);

    Task<int> MaterializePendingOccurrencesAsync(int actorId);
}
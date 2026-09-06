using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IRecurrenceRepository
{
    // Templates
    Task<TaskTemplate> CreateTemplateAsync(TaskTemplate template);
    Task<TaskTemplate?> GetTemplateByIdAsync(int id);
    Task<IEnumerable<TaskTemplate>> GetTemplatesByDepartmentAsync(int departmentId);
    Task<IEnumerable<TaskTemplate>> GetActiveTemplatesAsync();
    Task<TaskTemplate> UpdateTemplateAsync(TaskTemplate template);
    Task<bool> DeleteTemplateAsync(int id);

    // Recurrence Rules
    Task<RecurrenceRule> CreateRuleAsync(RecurrenceRule rule);
    Task<RecurrenceRule?> GetRuleByIdAsync(int id);
    Task<IEnumerable<RecurrenceRule>> GetRulesByTemplateIdAsync(int templateId);
    Task<IEnumerable<RecurrenceRule>> GetActiveRulesAsync();
    Task<RecurrenceRule> UpdateRuleAsync(RecurrenceRule rule);
    Task<bool> DeleteRuleAsync(int id);

    // Occurrences
    Task<RecurrenceOccurrence> CreateOccurrenceAsync(RecurrenceOccurrence occurrence);
    Task<RecurrenceOccurrence?> GetOccurrenceByIdAsync(int id);
    Task<IEnumerable<RecurrenceOccurrence>> GetOccurrencesByRuleIdAsync(int ruleId);
    Task<IEnumerable<RecurrenceOccurrence>> GetPendingOccurrencesAsync();
    Task<RecurrenceOccurrence> UpdateOccurrenceAsync(RecurrenceOccurrence occurrence);
    Task<bool> MarkOccurrenceGeneratedAsync(int occurrenceId, int taskId);
    Task<bool> MarkOccurrenceSkippedAsync(int occurrenceId, string? error = null);
    Task<bool> MarkOccurrenceCompletedAsync(int occurrenceId);
    Task<bool> OccurrenceExistsForDateAsync(int ruleId, DateTime date);

    // RC-3: atomically claim a PENDING occurrence for materialization.
    Task<RecurrenceOccurrence?> TryClaimPendingOccurrenceAsync(int occurrenceId);
}
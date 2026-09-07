using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class RecurrenceRepository : IRecurrenceRepository
{
    private readonly AppDbContext _context;

    public RecurrenceRepository(AppDbContext context)
    {
        _context = context;
    }

    // ============================================
    // Templates
    // ============================================

    public async Task<TaskTemplate> CreateTemplateAsync(TaskTemplate template)
    {
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        _context.TaskTemplates.Add(template);
        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<TaskTemplate?> GetTemplateByIdAsync(int id)
    {
        return await _context.TaskTemplates
            .Include(t => t.Department)
            .Include(t => t.CreatedBy)
            .Include(t => t.DefaultAssignee)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<TaskTemplate>> GetTemplatesByDepartmentAsync(int departmentId)
    {
        return await _context.TaskTemplates
            .Include(t => t.Department)
            .Include(t => t.DefaultAssignee)
            .Where(t => t.DepartmentId == departmentId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TaskTemplate>> GetActiveTemplatesAsync()
    {
        return await _context.TaskTemplates
            .Include(t => t.Department)
            .Include(t => t.DefaultAssignee)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<TaskTemplate> UpdateTemplateAsync(TaskTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _context.TaskTemplates.Update(template);
        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<bool> DeleteTemplateAsync(int id)
    {
        var template = await _context.TaskTemplates.FindAsync(id);
        if (template == null) return false;

        // Check if there are recurrence rules using this template
        var hasRules = await _context.RecurrenceRules.AnyAsync(r => r.TemplateId == id);
        if (hasRules) return false;

        _context.TaskTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }

    // ============================================
    // Recurrence Rules
    // ============================================

    public async Task<RecurrenceRule> CreateRuleAsync(RecurrenceRule rule)
    {
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
        _context.RecurrenceRules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task<RecurrenceRule?> GetRuleByIdAsync(int id)
    {
        return await _context.RecurrenceRules
            .Include(r => r.Template)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<RecurrenceRule>> GetRulesByTemplateIdAsync(int templateId)
    {
        return await _context.RecurrenceRules
            .Include(r => r.Template)
            .Where(r => r.TemplateId == templateId)
            .OrderBy(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<RecurrenceRule>> GetActiveRulesAsync()
    {
        // Compare by date only (UTC), not exact time-of-day: a rule whose
        // start date is "today" should be active for all of today on the
        // server, regardless of what clock time was picked in the form.
        var today = DateTime.UtcNow.Date;
        return await _context.RecurrenceRules
            .Include(r => r.Template)
            .Where(r => !r.IsPaused &&
                        r.StartDate.Date <= today &&
                        (!r.EndDate.HasValue || r.EndDate.Value.Date >= today))
            .OrderBy(r => r.NextGenerationAt)
            .ToListAsync();
    }

    public async Task<RecurrenceRule> UpdateRuleAsync(RecurrenceRule rule)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        _context.RecurrenceRules.Update(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        var rule = await _context.RecurrenceRules.FindAsync(id);
        if (rule == null) return false;
        _context.RecurrenceRules.Remove(rule);
        await _context.SaveChangesAsync();
        return true;
    }

    // ============================================
    // Occurrences
    // ============================================

    public async Task<RecurrenceOccurrence> CreateOccurrenceAsync(RecurrenceOccurrence occurrence)
    {
        occurrence.CreatedAt = DateTime.UtcNow;
        occurrence.UpdatedAt = DateTime.UtcNow;
        _context.RecurrenceOccurrences.Add(occurrence);
        await _context.SaveChangesAsync();
        return occurrence;
    }

    public async Task<RecurrenceOccurrence?> GetOccurrenceByIdAsync(int id)
    {
        return await _context.RecurrenceOccurrences
            .Include(o => o.Rule)
            .Include(o => o.Task)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<RecurrenceOccurrence>> GetOccurrencesByRuleIdAsync(int ruleId)
    {
        return await _context.RecurrenceOccurrences
            .Include(o => o.Task)
            .Where(o => o.RuleId == ruleId)
            .OrderBy(o => o.OccurrenceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<RecurrenceOccurrence>> GetPendingOccurrencesAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.RecurrenceOccurrences
            .Include(o => o.Rule)
            .Where(o => o.State == "PENDING" && o.OccurrenceDate <= now)
            .OrderBy(o => o.OccurrenceDate)
            .ToListAsync();
    }

    public async Task<RecurrenceOccurrence> UpdateOccurrenceAsync(RecurrenceOccurrence occurrence)
    {
        occurrence.UpdatedAt = DateTime.UtcNow;
        _context.RecurrenceOccurrences.Update(occurrence);
        await _context.SaveChangesAsync();
        return occurrence;
    }

    public async Task<bool> MarkOccurrenceGeneratedAsync(int occurrenceId, int taskId)
    {
        var occurrence = await _context.RecurrenceOccurrences.FindAsync(occurrenceId);
        if (occurrence == null) return false;

        occurrence.State = "GENERATED";
        occurrence.TaskId = taskId;
        occurrence.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkOccurrenceSkippedAsync(int occurrenceId, string? error = null)
    {
        var occurrence = await _context.RecurrenceOccurrences.FindAsync(occurrenceId);
        if (occurrence == null) return false;

        occurrence.State = "SKIPPED";
        occurrence.Error = error;
        occurrence.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkOccurrenceCompletedAsync(int occurrenceId)
    {
        var occurrence = await _context.RecurrenceOccurrences.FindAsync(occurrenceId);
        if (occurrence == null) return false;

        occurrence.State = "COMPLETED";
        occurrence.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> OccurrenceExistsForDateAsync(int ruleId, DateTime date)
    {
        return await _context.RecurrenceOccurrences
            .AnyAsync(o => o.RuleId == ruleId && o.OccurrenceDate.Date == date.Date);
    }

    public async Task<RecurrenceOccurrence?> TryClaimPendingOccurrenceAsync(int occurrenceId)
    {
        var updated = await _context.RecurrenceOccurrences
            .Where(o => o.Id == occurrenceId && o.State == "PENDING")
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.State, "MATERIALIZING")
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow));

        if (updated == 0) return null;

        return await _context.RecurrenceOccurrences.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == occurrenceId);
    }
}
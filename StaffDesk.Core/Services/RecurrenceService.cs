using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using System.Globalization;

namespace StaffDesk.Core.Services;

public class RecurrenceService : IRecurrenceService
{
    private readonly IRecurrenceRepository _recurrenceRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ITaskService _taskService;

    public RecurrenceService(
        IRecurrenceRepository recurrenceRepository,
        ITaskRepository taskRepository,
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        ITaskService taskService)
    {
        _recurrenceRepository = recurrenceRepository;
        _taskRepository = taskRepository;
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _taskService = taskService;
    }

    // ============================================
    // Templates (RC-1)
    // ============================================

    public async Task<TaskTemplate> CreateTemplateAsync(
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
        int createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required");

        if (string.IsNullOrWhiteSpace(titlePattern))
            throw new ArgumentException("Title pattern is required");

        var validPriorities = new[] { "URGENT", "HIGH", "NORMAL", "LOW" };
        if (!validPriorities.Contains(defaultPriority))
            throw new ArgumentException($"Priority must be one of: {string.Join(", ", validPriorities)}");

        var department = await _departmentRepository.GetByIdAsync(departmentId);
        if (department == null)
            throw new ArgumentException("Department not found");

        if (defaultAssigneeId.HasValue)
        {
            var assignee = await _employeeRepository.GetByIdAsync(defaultAssigneeId.Value);
            if (assignee == null)
                throw new ArgumentException("Default assignee not found");
            if (!assignee.IsActive)
                throw new ArgumentException("Default assignee is not active");
            if (assignee.DepartmentId != departmentId)
                throw new ArgumentException("Default assignee must belong to the department");
        }

        var template = new TaskTemplate
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            TitlePattern = titlePattern.Trim(),
            DefaultDescription = defaultDescription?.Trim(),
            DefaultPriority = defaultPriority,
            DefaultAssigneeId = defaultAssigneeId,
            DefaultAssignmentRule = defaultAssignmentRule,
            DefaultEstimateMinutes = defaultEstimateMinutes,
            DefaultTags = defaultTags ?? new List<string>(),
            DefaultChecklistItems = defaultChecklistItems ?? new List<string>(),
            DefaultAcceptanceCriteria = defaultAcceptanceCriteria ?? new List<string>(),
            DepartmentId = departmentId,
            CreatedById = createdBy,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _recurrenceRepository.CreateTemplateAsync(template);
    }

    public async Task<TaskTemplate?> GetTemplateByIdAsync(int id)
    {
        return await _recurrenceRepository.GetTemplateByIdAsync(id);
    }

    public async Task<IEnumerable<TaskTemplate>> GetTemplatesByDepartmentAsync(int departmentId)
    {
        return await _recurrenceRepository.GetTemplatesByDepartmentAsync(departmentId);
    }

    public async Task<TaskTemplate> UpdateTemplateAsync(TaskTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        return await _recurrenceRepository.UpdateTemplateAsync(template);
    }

    public async Task<bool> DeleteTemplateAsync(int id)
    {
        return await _recurrenceRepository.DeleteTemplateAsync(id);
    }

    // ============================================
    // Recurrence Rules (RC-2)
    // ============================================

    public async Task<RecurrenceRule> CreateRuleAsync(
        int templateId,
        string frequency,
        string? daysOfWeek,
        string? dayOfMonth,
        DateTime startDate,
        DateTime? endDate,
        string timezone,
        bool generateOnlyWhenPreviousComplete,
        bool isPaused,
        string nonWorkingDayPolicy = "NEXT_WORKING_DAY")
    {
        var validFrequencies = new[] { "DAILY", "WEEKLY", "MONTHLY" };
        if (!validFrequencies.Contains(frequency))
            throw new ArgumentException($"Frequency must be one of: {string.Join(", ", validFrequencies)}");

        var validPolicies = new[] { "SKIP", "NEXT_WORKING_DAY", "PREVIOUS_WORKING_DAY" };
        if (!validPolicies.Contains(nonWorkingDayPolicy))
            throw new ArgumentException($"NonWorkingDayPolicy must be one of: {string.Join(", ", validPolicies)}");

        if (frequency == "WEEKLY" && string.IsNullOrWhiteSpace(daysOfWeek))
            throw new ArgumentException("Days of week are required for weekly frequency");

        if (frequency == "MONTHLY" && string.IsNullOrWhiteSpace(dayOfMonth))
            throw new ArgumentException("Day of month is required for monthly frequency");

        // Validated here rather than left to fail at generation time - an invalid value (e.g.
        // "MON" instead of a numeric 0-6 code) previously reached an unguarded int.Parse deep in
        // occurrence generation and surfaced as an unhandled 500 instead of a 400.
        if (!string.IsNullOrWhiteSpace(daysOfWeek))
        {
            var parts = daysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Any(p => !int.TryParse(p, out var d) || d < 0 || d > 6))
                throw new ArgumentException("daysOfWeek must be a comma-separated list of numeric day codes (0=Sunday .. 6=Saturday)");
        }

        if (!string.IsNullOrWhiteSpace(dayOfMonth))
        {
            if (!int.TryParse(dayOfMonth, out var dom) || dom < 1 || dom > 31)
                throw new ArgumentException("dayOfMonth must be a numeric value between 1 and 31");
        }

        var template = await _recurrenceRepository.GetTemplateByIdAsync(templateId);
        if (template == null)
            throw new ArgumentException("Template not found");

        var rule = new RecurrenceRule
        {
            TemplateId = templateId,
            Frequency = frequency,
            DaysOfWeek = daysOfWeek,
            DayOfMonth = dayOfMonth,
            StartDate = startDate.ToUniversalTime(),
            EndDate = endDate?.ToUniversalTime(),
            Timezone = timezone,
            GenerateOnlyWhenPreviousComplete = generateOnlyWhenPreviousComplete,
            IsPaused = isPaused,
            NonWorkingDayPolicy = nonWorkingDayPolicy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdRule = await _recurrenceRepository.CreateRuleAsync(rule);

        // Generate initial occurrences
        await CreateOccurrencesForRuleAsync(createdRule.Id);

        return createdRule;
    }

    public async Task<RecurrenceRule?> GetRuleByIdAsync(int id)
    {
        return await _recurrenceRepository.GetRuleByIdAsync(id);
    }

    public async Task<RecurrenceRule> UpdateRuleAsync(RecurrenceRule rule)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        return await _recurrenceRepository.UpdateRuleAsync(rule);
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        return await _recurrenceRepository.DeleteRuleAsync(id);
    }

    public async Task<bool> PauseRuleAsync(int id)
    {
        var rule = await _recurrenceRepository.GetRuleByIdAsync(id);
        if (rule == null) return false;

        rule.IsPaused = true;
        rule.UpdatedAt = DateTime.UtcNow;
        await _recurrenceRepository.UpdateRuleAsync(rule);
        return true;
    }

    public async Task<bool> ResumeRuleAsync(int id)
    {
        var rule = await _recurrenceRepository.GetRuleByIdAsync(id);
        if (rule == null) return false;

        rule.IsPaused = false;
        rule.UpdatedAt = DateTime.UtcNow;
        await _recurrenceRepository.UpdateRuleAsync(rule);
        return true;
    }

    // ============================================
    // Occurrence Generation
    // ============================================

    public async Task<int> CreateOccurrencesForRuleAsync(int ruleId)
{
    var rule = await _recurrenceRepository.GetRuleByIdAsync(ruleId);
    if (rule == null) return 0;

    var created = 0;
    var currentDate = DateTime.UtcNow.Date;

    // If the rule has existing occurrences, start from the last one
    var existingOccurrences = await _recurrenceRepository.GetOccurrencesByRuleIdAsync(ruleId);
    if (existingOccurrences.Any())
    {
        currentDate = existingOccurrences.Max(o => o.OccurrenceDate).Date;
    }

    // Generate up to 10 future occurrences
    for (int i = 0; i < 10; i++)
    {
        // Calculate the next occurrence date based on frequency
        DateTime? nextDate = null;
        
        switch (rule.Frequency)
        {
            case "DAILY":
                nextDate = currentDate.AddDays(1);
                break;
            case "WEEKLY":
                nextDate = GetNextDayOfWeek(currentDate, rule.DaysOfWeek);
                break;
            case "MONTHLY":
                nextDate = GetNextDayOfMonth(currentDate, rule.DayOfMonth);
                break;
            default:
                return created;
        }

        if (!nextDate.HasValue) break;
        if (rule.EndDate.HasValue && nextDate.Value > rule.EndDate.Value) break;

        nextDate = ApplyNonWorkingDayPolicy(nextDate.Value, rule.NonWorkingDayPolicy);
        if (!nextDate.HasValue) { currentDate = currentDate.AddDays(1); continue; }

        // Check if occurrence already exists
        var exists = await _recurrenceRepository.OccurrenceExistsForDateAsync(ruleId, nextDate.Value);
        if (!exists)
        {
            var occurrence = new RecurrenceOccurrence
            {
                RuleId = ruleId,
                OccurrenceDate = nextDate.Value,
                State = "PENDING",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _recurrenceRepository.CreateOccurrenceAsync(occurrence);
            created++;
        }

        currentDate = nextDate.Value;
    }

    // Update the rule's next generation date
    rule.LastGeneratedAt = DateTime.UtcNow;
    rule.NextGenerationAt = currentDate.AddDays(1);
    await _recurrenceRepository.UpdateRuleAsync(rule);

    return created;
}
    public async Task<int> GenerateOccurrencesAsync()
    {
        var rules = await _recurrenceRepository.GetActiveRulesAsync();
        var totalGenerated = 0;

        foreach (var rule in rules)
        {
            var generated = await CreateOccurrencesForRuleAsync(rule.Id);
            totalGenerated += generated;
        }

        return totalGenerated;
    }

    public async Task<IEnumerable<RecurrenceOccurrence>> GetPendingOccurrencesAsync()
    {
        return await _recurrenceRepository.GetPendingOccurrencesAsync();
    }

    public async Task<IEnumerable<RecurrenceOccurrence>> GetOccurrencesByRuleIdAsync(int ruleId)
    {
        return await _recurrenceRepository.GetOccurrencesByRuleIdAsync(ruleId);
    }

    // ============================================
    // Calculate Next Occurrence (RC-2)
    // ============================================

    public DateTime? CalculateNextOccurrence(RecurrenceRule rule, DateTime? fromDate = null)
    {
        var start = fromDate ?? rule.StartDate;

        if (rule.EndDate.HasValue && start > rule.EndDate.Value)
            return null;

        if (rule.IsPaused)
            return null;

        return rule.Frequency switch
        {
            "DAILY" => start.AddDays(1),
            "WEEKLY" => CalculateNextWeeklyOccurrence(rule, start),
            "MONTHLY" => CalculateNextMonthlyOccurrence(rule, start),
            _ => null
        };
    }

    private DateTime? CalculateNextWeeklyOccurrence(RecurrenceRule rule, DateTime fromDate)
    {
        if (string.IsNullOrWhiteSpace(rule.DaysOfWeek))
            return null;

        var days = rule.DaysOfWeek.Split(',').Select(int.Parse).ToList();
        var current = fromDate.Date;

        for (int i = 0; i < 14; i++)
        {
            current = current.AddDays(i == 0 ? 0 : 1);
            if (days.Contains((int)current.DayOfWeek))
            {
                return current;
            }
        }

        return null;
    }

    private DateTime? CalculateNextMonthlyOccurrence(RecurrenceRule rule, DateTime fromDate)
    {
        if (string.IsNullOrWhiteSpace(rule.DayOfMonth))
            return null;

        var current = fromDate.Date;
        var day = int.TryParse(rule.DayOfMonth, out var dayOfMonth) ? dayOfMonth : 1;

        // Try current month
        var candidate = new DateTime(current.Year, current.Month, Math.Min(day, DateTime.DaysInMonth(current.Year, current.Month)));
        if (candidate > current)
            return candidate;

        // Try next month
        var nextMonth = current.AddMonths(1);
        candidate = new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
        return candidate;
    }

    private string ReplacePlaceholders(string text, DateTime date)
    {
        return text
            .Replace("{date}", date.ToString("yyyy-MM-dd"))
            .Replace("{date:short}", date.ToString("MMM dd, yyyy"))
            .Replace("{year}", date.Year.ToString())
            .Replace("{month}", date.Month.ToString())
            .Replace("{day}", date.Day.ToString())
            .Replace("{week}", CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstDay, DayOfWeek.Monday).ToString());
    }

    private async Task<int?> GetRotationAssigneeAsync(int templateId)
    {
        var template = await _recurrenceRepository.GetTemplateByIdAsync(templateId);
        if (template == null) return null;

        var employees = await _employeeRepository.GetByDepartmentIdAsync(template.DepartmentId);
        var activeEmployees = employees.Where(e => e.IsActive).ToList();

        if (!activeEmployees.Any()) return null;

        return activeEmployees.First().Id;
    }

    private async Task<RecurrenceOccurrence?> GetPreviousOccurrenceAsync(int ruleId, DateTime currentDate)
    {
        var occurrences = await _recurrenceRepository.GetOccurrencesByRuleIdAsync(ruleId);
        return occurrences
            .Where(o => o.OccurrenceDate < currentDate)
            .OrderByDescending(o => o.OccurrenceDate)
            .FirstOrDefault();
    }

    private DateTime? GetNextDayOfWeek(DateTime currentDate, string? daysOfWeek)
{
    if (string.IsNullOrWhiteSpace(daysOfWeek)) return null;

    var days = daysOfWeek.Split(',').Select(int.Parse).OrderBy(d => d).ToList();
    var current = currentDate.Date;

    for (int i = 0; i < 8; i++)
    {
        current = current.AddDays(i == 0 ? 1 : 0);
        if (days.Contains((int)current.DayOfWeek))
        {
            return current;
        }
        current = currentDate.Date; // Reset for next iteration
    }

    return null;
}

private DateTime? GetNextDayOfMonth(DateTime currentDate, string? dayOfMonth)
{
    if (string.IsNullOrWhiteSpace(dayOfMonth)) return null;

    var current = currentDate.Date.AddMonths(1);
    var day = int.TryParse(dayOfMonth, out var dayValue) ? dayValue : 1;

    var maxDay = DateTime.DaysInMonth(current.Year, current.Month);
    var targetDay = Math.Min(day, maxDay);

    return new DateTime(current.Year, current.Month, targetDay);
}

    public async Task<int> MaterializePendingOccurrencesAsync(int actorId)
    {
        var pending = (await _recurrenceRepository.GetPendingOccurrencesAsync()).ToList();
        var created = 0;

        foreach (var occurrence in pending)
        {
            var claimed = await _recurrenceRepository.TryClaimPendingOccurrenceAsync(occurrence.Id);
            if (claimed == null)
                continue;

            var rule = await _recurrenceRepository.GetRuleByIdAsync(claimed.RuleId);
            if (rule == null)
            {
                await _recurrenceRepository.MarkOccurrenceSkippedAsync(claimed.Id, "Rule not found");
                continue;
            }

            var template = rule.Template ?? await _recurrenceRepository.GetTemplateByIdAsync(rule.TemplateId);
            if (template == null || !template.IsActive)
            {
                await _recurrenceRepository.MarkOccurrenceSkippedAsync(claimed.Id, "Template not found or inactive");
                continue;
            }

            if (rule.GenerateOnlyWhenPreviousComplete)
            {
                var previous = await GetPreviousOccurrenceAsync(rule.Id, claimed.OccurrenceDate);
                if (previous?.TaskId != null)
                {
                    var prevTask = await _taskRepository.GetByIdAsync(previous.TaskId.Value);
                    if (prevTask != null && prevTask.Status != "DONE" && prevTask.Status != "CANCELLED")
                    {
                        continue;
                    }
                }
            }

            var department = await _departmentRepository.GetByIdAsync(template.DepartmentId);
            if (department == null)
            {
                await _recurrenceRepository.MarkOccurrenceSkippedAsync(claimed.Id, "Department not found");
                continue;
            }

            var title = ReplacePlaceholders(template.TitlePattern, claimed.OccurrenceDate);
            int? assigneeId = template.DefaultAssigneeId;
            if (assigneeId == null && template.DefaultAssignmentRule == "ROTATION")
                assigneeId = await GetRotationAssigneeAsync(template.Id);

            try
            {
                var task = await _taskService.CreateTaskAsync(
                    title,
                    template.DefaultDescription,
                    department.Name,
                    actorId,
                    assigneeId,
                    template.DefaultPriority,
                    null,
                    template.DefaultEstimateMinutes,
                    null,
                    template.DefaultTags);

                var position = 0;
                foreach (var item in template.DefaultChecklistItems)
                {
                    await _taskRepository.AddChecklistItemAsync(task.Id, item, position++);
                }

                position = 0;
                foreach (var criterion in template.DefaultAcceptanceCriteria)
                {
                    await _taskRepository.AddAcceptanceCriterionAsync(task.Id, criterion, position++);
                }

                await _recurrenceRepository.MarkOccurrenceGeneratedAsync(claimed.Id, task.Id);
                created++;
            }
            catch (Exception ex)
            {
                await _recurrenceRepository.MarkOccurrenceSkippedAsync(claimed.Id, ex.Message);
            }
        }

        return created;
    }

    private static DateTime? ApplyNonWorkingDayPolicy(DateTime date, string policy)
    {
        if (IsWorkingDay(date)) return date.Date;

        return policy switch
        {
            "SKIP" => null,
            "PREVIOUS_WORKING_DAY" => ShiftToPreviousWorkingDay(date),
            _ => ShiftToNextWorkingDay(date)
        };
    }

    private static bool IsWorkingDay(DateTime date)
    {
        return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    private static DateTime ShiftToNextWorkingDay(DateTime date)
    {
        var current = date.Date;
        for (var i = 0; i < 7; i++)
        {
            if (IsWorkingDay(current)) return current;
            current = current.AddDays(1);
        }
        return date.Date;
    }

    private static DateTime ShiftToPreviousWorkingDay(DateTime date)
    {
        var current = date.Date;
        for (var i = 0; i < 7; i++)
        {
            if (IsWorkingDay(current)) return current;
            current = current.AddDays(-1);
        }
        return date.Date;
    }
}
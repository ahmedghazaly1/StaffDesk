using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Services;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/recurrence")]
[Authorize]
public class RecurrenceController : ApiControllerBase
{
    private readonly IRecurrenceService _recurrenceService;
    private readonly IRecurrenceRepository _recurrenceRepository;  // ADDED

    public RecurrenceController(
        IRecurrenceService recurrenceService,
        IRecurrenceRepository recurrenceRepository,  // ADDED
        IUserRepository userRepository)
        : base(userRepository)
    {
        _recurrenceService = recurrenceService;
        _recurrenceRepository = recurrenceRepository;  // ADDED
    }

    // ============================================
    // Templates
    // ============================================

    // POST /v1/recurrence/templates - Create template (RC-1)
    [HttpPost("templates")]
    [AdminOnly]
    public async Task<IActionResult> CreateTemplate([FromBody] TaskTemplateCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var template = await _recurrenceService.CreateTemplateAsync(
                dto.Name,
                dto.Description,
                dto.TitlePattern,
                dto.DefaultDescription,
                dto.DefaultPriority,
                dto.DefaultAssigneeId,
                dto.DefaultAssignmentRule,
                dto.DefaultEstimateMinutes,
                dto.DefaultTags,
                dto.DefaultChecklistItems,
                dto.DefaultAcceptanceCriteria,
                dto.DepartmentId,
                employeeId.Value
            );

            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, MapTemplateToResponse(template));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // GET /v1/recurrence/templates - List templates
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] int? departmentId = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        IEnumerable<TaskTemplate> templates;
        if (departmentId.HasValue)
        {
            templates = await _recurrenceService.GetTemplatesByDepartmentAsync(departmentId.Value);
        }
        else
        {
            // Get all active templates
            templates = await _recurrenceRepository.GetActiveTemplatesAsync();
        }

        return Ok(templates.Select(MapTemplateToResponse));
    }

    // GET /v1/recurrence/templates/{id} - Get template
    [HttpGet("templates/{id}")]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var template = await _recurrenceService.GetTemplateByIdAsync(id);
        if (template == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Template not found"));

        return Ok(MapTemplateToResponse(template));
    }

    // PUT /v1/recurrence/templates/{id} - Update template
    [HttpPut("templates/{id}")]
    [AdminOnly]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] TaskTemplateUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var template = await _recurrenceService.GetTemplateByIdAsync(id);
        if (template == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Template not found"));

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.TitlePattern = dto.TitlePattern;
        template.DefaultDescription = dto.DefaultDescription;
        template.DefaultPriority = dto.DefaultPriority;
        template.DefaultAssigneeId = dto.DefaultAssigneeId;
        template.DefaultAssignmentRule = dto.DefaultAssignmentRule;
        template.DefaultEstimateMinutes = dto.DefaultEstimateMinutes;
        template.DefaultTags = dto.DefaultTags;
        template.DefaultChecklistItems = dto.DefaultChecklistItems;
        template.DefaultAcceptanceCriteria = dto.DefaultAcceptanceCriteria;
        template.IsActive = dto.IsActive;

        var updated = await _recurrenceService.UpdateTemplateAsync(template);
        return Ok(MapTemplateToResponse(updated));
    }

    // DELETE /v1/recurrence/templates/{id} - Delete template
    [HttpDelete("templates/{id}")]
    [AdminOnly]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var deleted = await _recurrenceService.DeleteTemplateAsync(id);
        if (!deleted)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Template not found or has recurrence rules"));

        return NoContent();
    }

    // ============================================
    // Recurrence Rules
    // ============================================

    // POST /v1/recurrence/rules - Create recurrence rule (RC-2)
    [HttpPost("rules")]
    [AdminOnly]
    public async Task<IActionResult> CreateRule([FromBody] RecurrenceRuleCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var rule = await _recurrenceService.CreateRuleAsync(
                dto.TemplateId,
                dto.Frequency,
                dto.DaysOfWeek,
                dto.DayOfMonth,
                dto.StartDate,
                dto.EndDate,
                dto.Timezone,
                dto.GenerateOnlyWhenPreviousComplete,
                dto.IsPaused,
                dto.NonWorkingDayPolicy
            );

            return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, MapRuleToResponse(rule));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, ex.Message));
        }
    }

    // GET /v1/recurrence/rules - List rules
    [HttpGet("rules")]
    public async Task<IActionResult> GetRules([FromQuery] int? templateId = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        IEnumerable<RecurrenceRule> rules;
        if (templateId.HasValue)
        {
            rules = await _recurrenceRepository.GetRulesByTemplateIdAsync(templateId.Value);
        }
        else
        {
            rules = await _recurrenceRepository.GetActiveRulesAsync();
        }

        return Ok(rules.Select(MapRuleToResponse));
    }

    // GET /v1/recurrence/rules/{id} - Get rule
    [HttpGet("rules/{id}")]
    public async Task<IActionResult> GetRule(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var rule = await _recurrenceService.GetRuleByIdAsync(id);
        if (rule == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Recurrence rule not found"));

        return Ok(MapRuleToResponse(rule));
    }

    // PUT /v1/recurrence/rules/{id} - Update rule
    [HttpPut("rules/{id}")]
    [AdminOnly]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] RecurrenceRuleUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var rule = await _recurrenceService.GetRuleByIdAsync(id);
        if (rule == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Recurrence rule not found"));

        rule.Frequency = dto.Frequency;
        rule.DaysOfWeek = dto.DaysOfWeek;
        rule.DayOfMonth = dto.DayOfMonth;
        rule.StartDate = RecurrenceService.NormalizeToUtc(dto.StartDate);
        rule.EndDate = dto.EndDate.HasValue ? RecurrenceService.NormalizeToUtc(dto.EndDate.Value) : null;
        rule.Timezone = dto.Timezone;
        rule.GenerateOnlyWhenPreviousComplete = dto.GenerateOnlyWhenPreviousComplete;
        rule.IsPaused = dto.IsPaused;

        var updated = await _recurrenceService.UpdateRuleAsync(rule);
        return Ok(MapRuleToResponse(updated));
    }

    // DELETE /v1/recurrence/rules/{id} - Delete rule
    [HttpDelete("rules/{id}")]
    [AdminOnly]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var deleted = await _recurrenceService.DeleteRuleAsync(id);
        if (!deleted)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Recurrence rule not found"));

        return NoContent();
    }

    // POST /v1/recurrence/rules/{id}/pause - Pause rule (RC-6)
    [HttpPost("rules/{id}/pause")]
    [AdminOnly]
    public async Task<IActionResult> PauseRule(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var paused = await _recurrenceService.PauseRuleAsync(id);
        if (!paused)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Recurrence rule not found"));

        return Ok(new { id, isPaused = true });
    }

    // POST /v1/recurrence/rules/{id}/resume - Resume rule
    [HttpPost("rules/{id}/resume")]
    [AdminOnly]
    public async Task<IActionResult> ResumeRule(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var resumed = await _recurrenceService.ResumeRuleAsync(id);
        if (!resumed)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Recurrence rule not found"));

        return Ok(new { id, isPaused = false });
    }

    // ============================================
    // Occurrences
    // ============================================

    // POST /v1/recurrence/generate - Generate pending occurrences (RC-3)
    [HttpPost("generate")]
    [AdminOnly]
    public async Task<IActionResult> GenerateOccurrences()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var generated = await _recurrenceService.GenerateOccurrencesAsync(employeeId.Value);
        return Ok(new { generated, message = $"Created {generated} task(s) from recurrence" });
    }

    // GET /v1/recurrence/rules/{ruleId}/occurrences - Get occurrences for a rule
    [HttpGet("rules/{ruleId}/occurrences")]
    public async Task<IActionResult> GetOccurrences(int ruleId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var occurrences = await _recurrenceService.GetOccurrencesByRuleIdAsync(ruleId);
        return Ok(occurrences.Select(MapOccurrenceToResponse));
    }

    // GET /v1/recurrence/occurrences/pending - Get pending occurrences
    [HttpGet("occurrences/pending")]
    [AdminOnly]
    public async Task<IActionResult> GetPendingOccurrences()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var occurrences = await _recurrenceService.GetPendingOccurrencesAsync();
        return Ok(occurrences.Select(MapOccurrenceToResponse));
    }

    // POST /v1/recurrence/materialize - Create tasks from pending occurrences (RC-6)
    [HttpPost("materialize")]
    [AdminOnly]
    public async Task<IActionResult> MaterializeOccurrences()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var created = await _recurrenceService.MaterializePendingOccurrencesAsync(employeeId.Value);
        return Ok(new { created, message = $"Materialized {created} tasks from pending occurrences" });
    }

    // ============================================
    // Helper Methods
    // ============================================

    private TaskTemplateResponseDto MapTemplateToResponse(TaskTemplate template)
    {
        return new TaskTemplateResponseDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            TitlePattern = template.TitlePattern,
            DefaultDescription = template.DefaultDescription,
            DefaultPriority = template.DefaultPriority,
            DefaultAssigneeId = template.DefaultAssigneeId,
            DefaultAssigneeName = template.DefaultAssignee?.FullName,
            DefaultAssignmentRule = template.DefaultAssignmentRule,
            DefaultEstimateMinutes = template.DefaultEstimateMinutes,
            DefaultTags = template.DefaultTags,
            DefaultChecklistItems = template.DefaultChecklistItems,
            DefaultAcceptanceCriteria = template.DefaultAcceptanceCriteria,
            DepartmentId = template.DepartmentId,
            DepartmentName = template.Department?.Name ?? string.Empty,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    private RecurrenceRuleResponseDto MapRuleToResponse(RecurrenceRule rule)
    {
        return new RecurrenceRuleResponseDto
        {
            Id = rule.Id,
            TemplateId = rule.TemplateId,
            TemplateName = rule.Template?.Name ?? string.Empty,
            Frequency = rule.Frequency,
            DaysOfWeek = rule.DaysOfWeek,
            DayOfMonth = rule.DayOfMonth,
            StartDate = rule.StartDate,
            EndDate = rule.EndDate,
            Timezone = rule.Timezone,
            GenerateOnlyWhenPreviousComplete = rule.GenerateOnlyWhenPreviousComplete,
            IsPaused = rule.IsPaused,
            NonWorkingDayPolicy = rule.NonWorkingDayPolicy,
            LastGeneratedAt = rule.LastGeneratedAt,
            NextGenerationAt = rule.NextGenerationAt,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }

    private RecurrenceOccurrenceResponseDto MapOccurrenceToResponse(RecurrenceOccurrence occurrence)
    {
        return new RecurrenceOccurrenceResponseDto
        {
            Id = occurrence.Id,
            RuleId = occurrence.RuleId,
            TaskId = occurrence.TaskId,
            TaskKey = occurrence.Task?.Key,
            OccurrenceDate = occurrence.OccurrenceDate,
            State = occurrence.State,
            Error = occurrence.Error,
            CreatedAt = occurrence.CreatedAt,
            UpdatedAt = occurrence.UpdatedAt
        };
    }
}
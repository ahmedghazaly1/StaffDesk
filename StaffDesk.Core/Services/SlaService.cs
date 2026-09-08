using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class SlaService : ISlaService
{
    private readonly ISlaRepository _slaRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IWorkingCalendarService _calendarService;
    private readonly IAuditService _auditService;  // ADDED

    // SL-6: escalation schedule - level 0 = assignee, level 1 = department manager, level 2 = manager's manager
    private readonly List<(int Level, int DelayMinutes, string Target)> _escalationLevels = new()
    {
        (0, 0, "Assignee"),           // Immediate on breach
        (1, 60, "Department Manager"), // 60 working minutes after breach
        (2, 120, "Manager's Manager")  // 120 working minutes after breach
    };

    public SlaService(
        ISlaRepository slaRepository,
        ITaskRepository taskRepository,
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        IWorkingCalendarService calendarService,
        IAuditService auditService)  // ADDED
    {
        _slaRepository = slaRepository;
        _taskRepository = taskRepository;
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _calendarService = calendarService;
        _auditService = auditService;  // ADDED
    }

    public List<(int Level, int DelayMinutes, string Target)> GetEscalationLevels()
    {
        return _escalationLevels;
    }

    public async Task<WorkTask> UpdateSlaStateAsync(WorkTask task)
    {
        // SL-7: changing priority recomputes targets from the moment of change
        var policy = await _slaRepository.GetPolicyAsync(task.DepartmentId, task.Priority);
        if (policy == null)
        {
            // No SLA policy - clear SLA fields
            task.ResponseTargetAt = null;
            task.ResolutionTargetAt = null;
            task.BreachState = null;
            return task;
        }

        // SL-8: SLA state is derived - never directly editable
        // Calculate targets from creation time
        var startedAt = task.CreatedAt;

        // Response target: time from creation to first assignment (or current time if unassigned)
        if (task.AssigneeId.HasValue)
        {
            task.ResponseTargetAt = _calendarService.AddWorkingMinutes(
                startedAt, 
                policy.ResponseTargetMinutes, 
                task.DepartmentId
            );
        }

        if (task.Status == "IN_PROGRESS" || task.Status == "IN_REVIEW" || task.Status == "DONE")
        {
            var acceptanceTime = await GetAcceptanceTimeAsync(task.Id);
            var actualStart = acceptanceTime ?? startedAt;
            task.ResolutionTargetAt = _calendarService.AddWorkingMinutes(
                actualStart,
                policy.ResolutionTargetMinutes,
                task.DepartmentId
            );
        }

        // Determine breach state (SL-3: subtract blocked pause from remaining time)
        var blockedPause = await GetBlockedPauseMinutesAsync(task);
        task.BreachState = DetermineBreachState(task, blockedPause);

        return task;
    }

    public async Task RecordSlaSnapshotAsync(WorkTask task)
    {
        var policy = await _slaRepository.GetPolicyAsync(task.DepartmentId, task.Priority);
        if (policy == null) return;

        await _slaRepository.AppendSlaRecordAsync(new TaskSlaRecord
        {
            TaskId = task.Id,
            Priority = task.Priority,
            ResponseTargetMinutes = policy.ResponseTargetMinutes,
            ResolutionTargetMinutes = policy.ResolutionTargetMinutes,
            EffectiveFrom = DateTime.UtcNow
        });
    }

    public async Task<int> GetBlockedPauseMinutesAsync(int taskId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return 0;

        return await GetBlockedPauseMinutesAsync(task);
    }

    // Overload for callers that already hold the task, so list endpoints don't re-read every row.
    public async Task<int> GetBlockedPauseMinutesAsync(WorkTask task)
    {
        var total = task.BlockedPauseMinutes;

        // Only the current status interval is left open, so a task that isn't BLOCKED right now
        // cannot have running blocked time to add — no need to read its intervals at all.
        if (task.Status != "BLOCKED") return total;

        var intervals = await _taskRepository.GetStatusIntervalsAsync(task.Id);
        var openBlocked = intervals.FirstOrDefault(i => i.Status == "BLOCKED" && i.ExitedAt == null);
        if (openBlocked != null)
        {
            total += (int)_calendarService.GetWorkingMinutes(
                openBlocked.EnteredAt, DateTime.UtcNow, task.DepartmentId);
        }

        return total;
    }

    // ============================================
    // Evaluate all tasks and return results
    // ============================================
    public async Task<List<SlaEvaluationResult>> EvaluateAllTasksAsync()
    {
        var results = new List<SlaEvaluationResult>();
        
        // Get all active tasks
        var activeStatuses = new[] { "OPEN", "IN_PROGRESS", "BLOCKED", "IN_REVIEW" };
        var result = await _taskRepository.GetFilteredAsync(
            0, null, true,
            status: string.Join(",", activeStatuses),
            includeArchived: true
        );

        foreach (var task in result.Items)
        {
            var previousState = task.BreachState;
            
            // Update SLA state
            await UpdateSlaStateAsync(task);
            await _taskRepository.UpdateAsync(task);

            // Process escalations
            var escalationsSent = 0;
            if (task.BreachState == "BREACHED" || task.BreachState == "AT_RISK")
            {
                await ProcessEscalationsForTaskAsync(task);
                escalationsSent = (await _slaRepository.GetEscalationsForTaskAsync(task.Id)).Count();
            }

            results.Add(new SlaEvaluationResult
            {
                TaskId = task.Id,
                TaskKey = task.Key,
                PreviousState = previousState,
                NewState = task.BreachState,
                EscalationsSent = escalationsSent,
                Reason = task.BreachState != previousState ? $"State changed from {previousState} to {task.BreachState}" : null
            });
        }

        return results;
    }

    public async Task ProcessEscalationsAsync()
    {
        // Find breached tasks that haven't had all escalations sent
        var breachedTasks = await GetBreachedTasksAsync();

        foreach (var task in breachedTasks)
        {
            await ProcessEscalationsForTaskAsync(task);
        }
    }

    // ============================================
    // Get SLA state for a task
    // ============================================
    public async Task<SlaTaskState> GetTaskSlaStateAsync(WorkTask task)
    {
        var escalations = await _slaRepository.GetEscalationsForTaskAsync(task.Id);
        var blockedPause = await GetBlockedPauseMinutesAsync(task.Id);
        var remaining = GetRemainingMinutes(task, blockedPause);

        return new SlaTaskState
        {
            ResponseTargetAt = task.ResponseTargetAt,
            ResolutionTargetAt = task.ResolutionTargetAt,
            BreachState = task.BreachState,
            RemainingMinutes = remaining,
            IsBreached = task.BreachState == "BREACHED",
            IsAtRisk = task.BreachState == "AT_RISK",
            Escalations = escalations.ToList()
        };
    }

    // ============================================
    // Log SLA event for audit - FIXED
    // ============================================
    public async Task LogSlaEventAsync(int actorId, string eventType, string targetId, string outcome, object? changes = null)
    {
        // Get the actor's label (name and role)
        var actor = await _employeeRepository.GetByIdAsync(actorId);
        var actorLabel = actor != null ? $"{actor.FullName} ({actor.JobTitle})" : "Unknown";

        // Always log to console for debugging
        Console.WriteLine($"SLA Event: {eventType} - Actor: {actorId} - Target: {targetId} - Outcome: {outcome}");

        // Write to audit log for security-relevant SLA events
        try
        {
            await _auditService.LogAsync(
                eventType: eventType,
                actorId: actorId,
                actorLabel: actorLabel,
                outcome: outcome,
                targetType: "SlaPolicy",
                targetId: targetId,
                changes: changes,
                sourceIp: null,  // Will be filled by middleware
                userAgent: null   // Will be filled by middleware
            );
        }
        catch (Exception ex)
        {
            // Log but don't fail the operation if audit fails
            Console.WriteLine($"Audit write failed (non-critical): {ex.Message}");
        }
    }

    private async Task ProcessEscalationsForTaskAsync(WorkTask task)
    {
        var breachTime = task.DueAt ?? DateTime.UtcNow;
        var now = DateTime.UtcNow;
        var minutesSinceBreach = _calendarService.GetWorkingMinutes(breachTime, now, task.DepartmentId);

        foreach (var (level, delayMinutes, target) in _escalationLevels)
        {
            // Check if this escalation level should fire
            if (minutesSinceBreach >= delayMinutes)
            {
                // Check if already sent
                var alreadySent = await _slaRepository.HasEscalationBeenSentAsync(task.Id, level);
                if (alreadySent) continue;

                // Get the target employee for this escalation level
                var targetEmployee = await GetEscalationTargetAsync(task, level);
                if (targetEmployee == null) continue;

                // Create notification
                await _taskRepository.CreateNotificationAsync(
                    targetEmployee.Id,
                    "SLA_BREACHED",
                    $"SLA breached for task {task.Key}: {task.Title} (Level {level + 1}: {target})",
                    task.Id
                );

                // Record escalation
                var escalation = new SlaEscalation
                {
                    TaskId = task.Id,
                    Level = level,
                    NotifiedAt = DateTime.UtcNow
                };
                await _slaRepository.AddEscalationAsync(escalation);
            }
        }
    }

    private async Task<Employee?> GetEscalationTargetAsync(WorkTask task, int level)
    {
        switch (level)
        {
            case 0: // Assignee
                if (task.AssigneeId.HasValue)
                    return await _employeeRepository.GetByIdAsync(task.AssigneeId.Value);
                return null;
            case 1: // Department manager
                var dept = await _departmentRepository.GetByIdAsync(task.DepartmentId);
                if (dept?.ManagerId.HasValue == true)
                    return await _employeeRepository.GetByIdAsync(dept.ManagerId.Value);
                return null;
            case 2: // Manager's manager
                dept = await _departmentRepository.GetByIdAsync(task.DepartmentId);
                if (dept?.ManagerId.HasValue == true)
                {
                    var manager = await _employeeRepository.GetByIdAsync(dept.ManagerId.Value);
                    if (manager?.ManagerId.HasValue == true)
                        return await _employeeRepository.GetByIdAsync(manager.ManagerId.Value);
                }
                return null;
            default:
                return null;
        }
    }

    private async Task<IEnumerable<WorkTask>> GetBreachedTasksAsync()
    {
        var activeStatuses = new[] { "OPEN", "IN_PROGRESS", "BLOCKED", "IN_REVIEW" };
        var result = await _taskRepository.GetFilteredAsync(
            0, null, true,
            status: string.Join(",", activeStatuses),
            includeArchived: true
        );

        var breachedTasks = new List<WorkTask>();
        foreach (var task in result.Items)
        {
            if (IsBreached(task))
                breachedTasks.Add(task);
        }
        return breachedTasks;
    }

    public bool IsBreached(WorkTask task)
    {
        if (task.BreachState == "BREACHED") return true;
        if (task.BreachState == "AT_RISK") return false;
        return false;
    }

    public long? GetRemainingMinutes(WorkTask task)
    {
        return GetRemainingMinutes(task, null);
    }

    public long? GetRemainingMinutes(WorkTask task, int? blockedPauseMinutes)
    {
        if (task.ResolutionTargetAt == null) return null;

        var pause = blockedPauseMinutes ?? 0;
        var remaining = SignedRemainingWorkingMinutes(task.ResolutionTargetAt.Value, pause, task.DepartmentId);
        return remaining > 0 ? remaining : 0;
    }

    /// <summary>
    /// Working minutes until the resolution target, plus blocked pause. Negative means the target is overdue.
    /// Calendar GetWorkingMinutes returns 0 when from &gt;= to, so overdue is measured as minutes from target to now.
    /// </summary>
    public long SignedRemainingWorkingMinutes(DateTime resolutionTargetAt, int blockedPauseMinutes, int departmentId)
    {
        var now = DateTime.UtcNow;
        var untilTarget = _calendarService.GetWorkingMinutes(now, resolutionTargetAt, departmentId);
        var overdue = _calendarService.GetWorkingMinutes(resolutionTargetAt, now, departmentId);
        return untilTarget - overdue + blockedPauseMinutes;
    }

    public string? DetermineBreachState(WorkTask task, int blockedPauseMinutes = 0)
    {
        // Terminal statuses don't have breach states
        if (task.Status == "DONE" || task.Status == "CANCELLED")
            return null;

        if (task.ResolutionTargetAt == null)
            return null;

        var remaining = SignedRemainingWorkingMinutes(task.ResolutionTargetAt.Value, blockedPauseMinutes, task.DepartmentId);

        if (remaining < 0)
            return "BREACHED";

        // AT_RISK threshold: 20% of total target time remaining, or 60 minutes whichever is less
        var totalMinutes = _calendarService.GetWorkingMinutes(
            task.CreatedAt,
            task.ResolutionTargetAt.Value,
            task.DepartmentId
        );
        var threshold = Math.Min(totalMinutes * 0.2, 60);

        if (remaining < threshold)
            return "AT_RISK";

        return "ON_TRACK";
    }

    private async Task<DateTime?> GetAssignmentTimeAsync(int taskId)
    {
        var activities = await _taskRepository.GetActivitiesAsync(taskId);
        var assignment = activities
            .FirstOrDefault(a => a.Action == "ASSIGNED" && a.NewValue != "null");
        return assignment?.CreatedAt;
    }

    private async Task<DateTime?> GetAcceptanceTimeAsync(int taskId)
    {
        var intervals = await _taskRepository.GetStatusIntervalsAsync(taskId);
        var firstInProgress = intervals
            .FirstOrDefault(i => i.Status == "IN_PROGRESS");
        return firstInProgress?.EnteredAt;
    }
}
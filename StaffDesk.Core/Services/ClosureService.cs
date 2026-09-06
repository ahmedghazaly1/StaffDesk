using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class ClosureService : IClosureService
{
    private readonly IClosureRepository _closureRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ISlaRepository _slaRepository;

    public ClosureService(
        IClosureRepository closureRepository,
        ITaskRepository taskRepository,
        ISlaRepository slaRepository)
    {
        _closureRepository = closureRepository;
        _taskRepository = taskRepository;
        _slaRepository = slaRepository;
    }

    // ============================================
    // Set Task Outcome (WC-15)
    // ============================================
    public async Task<TaskOutcome> SetOutcomeAsync(
        int taskId,
        string outcome,
        string? note,
        string? externalReference,
        string? externalReferenceLabel,
        int userId)
    {
        var validOutcomes = new[] { "DELIVERED", "DELIVERED_PARTIAL", "SUPERSEDED", "NOT_REPRODUCIBLE", "DUPLICATE", "WONT_DO" };
        if (!validOutcomes.Contains(outcome))
            throw new ArgumentException($"Outcome must be one of: {string.Join(", ", validOutcomes)}");

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new ArgumentException("Task not found");

        if (task.Status != "DONE" && task.Status != "CANCELLED")
            throw new InvalidOperationException("Outcome can only be set for tasks that are DONE or CANCELLED");

        // Check if outcome already exists
        var existing = await _closureRepository.GetOutcomeByTaskIdAsync(taskId);
        if (existing != null)
            throw new InvalidOperationException("Outcome already set for this task");

        var taskOutcome = new TaskOutcome
        {
            TaskId = taskId,
            Outcome = outcome,
            Note = note,
            ExternalReference = externalReference,
            ExternalReferenceLabel = externalReferenceLabel,
            OccurredAt = DateTime.UtcNow
        };

        var created = await _closureRepository.CreateOutcomeAsync(taskOutcome);

        // Update task with outcome
        task.Outcome = outcome;
        await _taskRepository.UpdateAsync(task);

        // Add activity
        await _taskRepository.AddActivityAsync(
            taskId,
            userId,
            "OUTCOME_SET",
            "Outcome",
            null,
            outcome,
            note,
            Guid.NewGuid().ToString()
        );

        return created;
    }

    // ============================================
    // Get Outcome by Task ID
    // ============================================
    public async Task<TaskOutcome?> GetOutcomeByTaskIdAsync(int taskId)
    {
        return await _closureRepository.GetOutcomeByTaskIdAsync(taskId);
    }

    // ============================================
    // Get All Outcomes by Task ID
    // ============================================
    public async Task<IEnumerable<TaskOutcome>> GetOutcomesByTaskIdAsync(int taskId)
    {
        return await _closureRepository.GetOutcomesByTaskIdAsync(taskId);
    }

    // ============================================
    // Process Task Closure (WC-17)
    // ============================================
    public async Task<TaskClosure> ProcessClosureAsync(int taskId, int userId, string? closureNote = null)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new ArgumentException("Task not found");

        if (task.Status != "DONE")
            throw new InvalidOperationException("Closure can only be processed for tasks that are DONE");

        // Check if closure already exists
        var existingClosure = await _closureRepository.GetClosureByTaskIdAsync(taskId);
        if (existingClosure != null)
            return existingClosure;

        // Determine if closure note is required (WC-17)
        var isSlaBreached = task.BreachState == "BREACHED";
        var wasReopened = task.ReopenCount > 0;
        var hasOverriddenAcceptance = false; // This would be set from acceptance criteria override

        var isClosureRequired = isSlaBreached || wasReopened || hasOverriddenAcceptance;

        // WC-17: validated here, before persistence, so the note is never silently discarded -
        // the controller previously validated dto.ClosureNote but never passed it through to the
        // entity, leaving TaskClosure.ClosureNote permanently null.
        if (isClosureRequired && (string.IsNullOrWhiteSpace(closureNote) || closureNote.Trim().Length < 10))
            throw new ArgumentException("Closure note is required (minimum 10 characters) for tasks that breached SLA or were reopened");

        var closure = new TaskClosure
        {
            TaskId = taskId,
            IsSlaBreached = isSlaBreached,
            WasReopened = wasReopened,
            HasOverriddenAcceptance = hasOverriddenAcceptance,
            ClosureNote = closureNote?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _closureRepository.CreateClosureAsync(closure);

        // Update task
        task.IsClosureRequired = isClosureRequired;
        await _taskRepository.UpdateAsync(task);

        // Add activity
        await _taskRepository.AddActivityAsync(
            taskId,
            userId,
            "CLOSURE_PROCESSED",
            null,
            null,
            $"SLA Breached: {isSlaBreached}, Reopened: {wasReopened}",
            null,
            Guid.NewGuid().ToString()
        );

        return created;
    }

    // ============================================
    // Get Closure by Task ID
    // ============================================
    public async Task<TaskClosure?> GetClosureByTaskIdAsync(int taskId)
    {
        return await _closureRepository.GetClosureByTaskIdAsync(taskId);
    }

    // ============================================
    // Check if Task Has Closure
    // ============================================
    public async Task<bool> HasClosureAsync(int taskId)
    {
        return await _closureRepository.HasClosureAsync(taskId);
    }
}
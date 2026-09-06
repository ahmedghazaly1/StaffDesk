using StaffDesk.Core.Constants;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;  // ADD THIS

namespace StaffDesk.Core.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ISeniorityLevelRepository _levelRepository;
    private readonly IUserRepository _userRepository;
    private readonly IReworkRepository _reworkRepository;
    private readonly ISlaService _slaService;
    private readonly IClosureRepository _closureRepository;
    private readonly ILeaveService _leaveService;
    private readonly ITimesheetService _timesheetService;

    private static readonly string[] ValidReworkCategories =
        { "incomplete", "defective", "misunderstood", "changed", "quality" };

    private static readonly string[] ValidOutcomes =
        { "DELIVERED", "DELIVERED_PARTIAL", "SUPERSEDED", "NOT_REPRODUCIBLE", "DUPLICATE", "WONT_DO" };

    // Status workflow - allowed transitions.
    // NOTE: IN_PROGRESS -> DONE removed (not in SRS matrix); only IN_REVIEW -> DONE may complete a task.
    private static readonly Dictionary<string, List<string>> _allowedTransitions = new()
    {
        { "OPEN", new List<string> { "IN_PROGRESS", "CANCELLED" } },
        { "IN_PROGRESS", new List<string> { "IN_REVIEW", "BLOCKED", "OPEN" } },
        { "BLOCKED", new List<string> { "IN_PROGRESS", "CANCELLED" } },
        { "IN_REVIEW", new List<string> { "DONE", "IN_PROGRESS" } },
        { "DONE", new List<string> { "OPEN" } },
        { "CANCELLED", new List<string> { "OPEN" } }
    };

    // Active statuses
    private static readonly List<string> _activeStatuses = new() { "OPEN", "IN_PROGRESS", "BLOCKED", "IN_REVIEW" };

    public TaskService(
        ITaskRepository taskRepository,
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        ISeniorityLevelRepository levelRepository,
        IUserRepository userRepository,
        IReworkRepository reworkRepository,
        ISlaService slaService,
        IClosureRepository closureRepository,
        ILeaveService leaveService,
        ITimesheetService timesheetService)
    {
        _taskRepository = taskRepository;
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _levelRepository = levelRepository;
        _userRepository = userRepository;
        _reworkRepository = reworkRepository;
        _slaService = slaService;
        _closureRepository = closureRepository;
        _leaveService = leaveService;
        _timesheetService = timesheetService;
    }

    // ============================================
    // CRUD Operations
    // ============================================

    public async Task<WorkTask> CreateTaskAsync(
        string title,
        string? description,
        string departmentName,
        int createdById,
        int? assigneeId = null,
        string priority = "NORMAL",
        DateTime? dueAt = null,
        int? estimateMinutes = null,
        int? parentTaskId = null,
        List<string>? tags = null)
    {
        // VE-6: trim before validating length, so whitespace-only titles fail as empty and
        // incidental leading/trailing whitespace doesn't count toward the length limit.
        title = title?.Trim() ?? string.Empty;

        // Validate - collect ALL failing fields for VALIDATION_ERROR.details
        var validationErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(title) || title.Length < 3 || title.Length > 140)
            validationErrors.Add("Title must be between 3 and 140 characters");

        if (estimateMinutes.HasValue && estimateMinutes.Value < 0)
            validationErrors.Add("Estimate must be a positive number");

        var validPriorities = new[] { "URGENT", "HIGH", "NORMAL", "LOW" };
        if (!validPriorities.Contains(priority))
            validationErrors.Add("Priority must be one of URGENT, HIGH, NORMAL, LOW");

        if (tags != null && tags.Count > 10)
            throw new TaskDomainException(TaskErrorCodes.LimitExceeded, "A task may have at most 10 tags", 422);

        // Find department by name (case-insensitive)
        var allDepartments = await _departmentRepository.GetAllAsync();
        var department = allDepartments.FirstOrDefault(d =>
            d.Name.Equals(departmentName, StringComparison.OrdinalIgnoreCase));

        if (department == null)
            validationErrors.Add($"Department '{departmentName}' does not exist");

        if (validationErrors.Any())
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Validation failed", 400, validationErrors);

        // Section 8.2 permission matrix: "Create a task in another department" is ADMIN-only;
        // MANAGER and MEMBER may only create within their own department. Resolved once here and
        // reused below for the URGENT-priority check too.
        var actorRole = await GetUserRoleAsync(createdById);
        if (actorRole != "Admin")
        {
            var actorEmployee = await _employeeRepository.GetByIdAsync(createdById);
            if (actorEmployee == null || actorEmployee.DepartmentId != department!.Id)
                throw new TaskDomainException(TaskErrorCodes.Forbidden,
                    "You may only create tasks in your own department", 403);
        }

        // Section 8.2 / TR-29: setting URGENT at creation time is subject to the same rule as
        // changing priority to URGENT later - ADMIN, the department manager, or rank >= Lead only.
        if (priority == "URGENT" && !await CanSetUrgentAsync(createdById, department!.Id))
            throw new TaskDomainException(TaskErrorCodes.AssignmentNotPermitted,
                "Only ADMIN, the department manager, or an employee with rank >= Lead may set URGENT priority", 403);

        // Check assignee if provided
        if (assigneeId.HasValue)
        {
            var assignee = await _employeeRepository.GetByIdAsync(assigneeId.Value);
            if (assignee == null)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Assignee does not exist", 400);
            if (!assignee.IsActive)
                throw new TaskDomainException(TaskErrorCodes.EmployeeInactive, "Assignee is not active", 422);
            if (assignee.DepartmentId != department!.Id)
                throw new TaskDomainException(TaskErrorCodes.CrossDepartment, "Assignee must belong to the task's department", 422);
        }

        // Check parent task if provided (TR-31/TR-32)
        if (parentTaskId.HasValue)
        {
            var parent = await _taskRepository.GetByIdAsync(parentTaskId.Value);
            if (parent == null)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Parent task does not exist", 400);
            if (parent.ParentTaskId.HasValue)
                throw new TaskDomainException(TaskErrorCodes.NestingTooDeep, "Task nesting is limited to one level", 422);
            if (parent.DepartmentId != department!.Id)
                throw new TaskDomainException(TaskErrorCodes.CrossDepartment, "Parent task must be in the same department", 422);
        }

        // TR-41: only ADMIN may create brand-new tags; anyone who can create a task may attach existing tags.
        var tagsToAttach = new List<Tag>();
        if (tags != null && tags.Any())
        {
            foreach (var tagName in tags)
            {
                var existingTag = await _taskRepository.FindTagByNameAsync(tagName);
                if (existingTag == null && actorRole != "Admin")
                    throw new TaskDomainException(TaskErrorCodes.TagCreationNotPermitted,
                        $"You do not have permission to create a new tag ('{tagName}')", 403);

                var tag = existingTag ?? await _taskRepository.GetOrCreateTagAsync(tagName);
                tagsToAttach.Add(tag);
            }
        }

        WorkTask createdTask = null!;

        await _taskRepository.ExecuteInTransactionAsync(async () =>
        {
            // Create task
            var task = new WorkTask
            {
                Title = title.Trim(),
                Description = description?.Trim(),
                DepartmentId = department!.Id,
                CreatedById = createdById,
                AssigneeId = assigneeId,
                Priority = priority,
                DueAt = dueAt?.ToUniversalTime(),
                EstimateMinutes = estimateMinutes,
                ParentTaskId = parentTaskId,
                Status = "OPEN",
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            createdTask = await _taskRepository.CreateAsync(task);

            foreach (var tag in tagsToAttach)
            {
                await _taskRepository.AddTagToTaskAsync(createdTask.Id, tag.Id);
            }

            // Add activity
            await _taskRepository.AddActivityAsync(
                createdTask.Id,
                createdById,
                "CREATED",
                null,
                null,
                null,
                null,
                Guid.NewGuid().ToString()
            );

            // WC-23: open the first status interval in the same transaction as the task insert.
            await _taskRepository.OpenStatusIntervalAsync(createdTask.Id, createdTask.Status, createdById, createdTask.CreatedAt);

            // WC-12: copy the department's default Definition of Done onto the new task.
            await _taskRepository.CopyDepartmentDefaultCriteriaAsync(createdTask.Id, department!.Id);

            // Add creator as watcher
            await _taskRepository.AddWatcherAsync(createdTask.Id, createdById);

            // If assignee is set and different from creator, add them as watcher
            if (assigneeId.HasValue && assigneeId.Value != createdById)
            {
                await _taskRepository.AddWatcherAsync(createdTask.Id, assigneeId.Value);

                await _taskRepository.CreateNotificationAsync(
                    assigneeId.Value,
                    "TASK_ASSIGNED",
                    $"You have been assigned to task {createdTask.Key}: {createdTask.Title}",
                    createdTask.Id
                );
            }
        });

        var persisted = await _taskRepository.GetByIdAsync(createdTask.Id) ?? createdTask;
        await _slaService.UpdateSlaStateAsync(persisted);
        await _slaService.RecordSlaSnapshotAsync(persisted);
        await _taskRepository.UpdateAsync(persisted);
        return persisted;
    }

    public async Task<WorkTask?> GetTaskByIdAsync(int id, int viewerId)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null) return null;

        if (!await CanReadTaskAsync(id, viewerId))
            return null;

        return task;
    }

    public async Task<WorkTask?> GetTaskByKeyAsync(string key, int viewerId)
    {
        var task = await _taskRepository.GetByKeyAsync(key);
        if (task == null) return null;

        if (!await CanReadTaskAsync(task.Id, viewerId))
            return null;

        return task;
    }

    public async Task<WorkTask> UpdateTaskAsync(
        int taskId,
        int userId,
        string title,
        string? description,
        string departmentName,
        int? assigneeId,
        string priority,
        DateTime? dueAt,
        int? estimateMinutes,
        int? parentTaskId,
        bool isArchived)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

        if (!await CanEditTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to edit this task", 403);

        // VE-6: trim before validating length (see CreateTaskAsync).
        title = title?.Trim() ?? string.Empty;

        var validationErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(title) || title.Length < 3 || title.Length > 140)
            validationErrors.Add("Title must be between 3 and 140 characters");

        if (estimateMinutes.HasValue && estimateMinutes.Value < 0)
            validationErrors.Add("Estimate must be a positive number");

        var validPriorities = new[] { "URGENT", "HIGH", "NORMAL", "LOW" };
        if (!validPriorities.Contains(priority))
            validationErrors.Add("Priority must be one of URGENT, HIGH, NORMAL, LOW");

        // Find department by name (case-insensitive)
        var allDepartments = await _departmentRepository.GetAllAsync();
        var department = allDepartments.FirstOrDefault(d =>
            d.Name.Equals(departmentName, StringComparison.OrdinalIgnoreCase));

        if (department == null)
            validationErrors.Add($"Department '{departmentName}' does not exist");

        if (validationErrors.Any())
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Validation failed", 400, validationErrors);

        if (assigneeId.HasValue)
        {
            var assignee = await _employeeRepository.GetByIdAsync(assigneeId.Value);
            if (assignee == null)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Assignee does not exist", 400);
            if (!assignee.IsActive)
                throw new TaskDomainException(TaskErrorCodes.EmployeeInactive, "Assignee is not active", 422);

            if (!await CanAssignAsync(userId, assigneeId, department!.Id, task.AssigneeId))
                throw new TaskDomainException(TaskErrorCodes.AssignmentNotPermitted,
                    "Only the department manager, ADMIN, or an employee with rank >= Lead in the same department may assign this task", 403);
        }

        // TR-31/TR-32: parent task nesting/department validation (also applies on update).
        if (parentTaskId.HasValue && parentTaskId != task.ParentTaskId)
        {
            var parent = await _taskRepository.GetByIdAsync(parentTaskId.Value);
            if (parent == null)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Parent task does not exist", 400);
            if (parent.ParentTaskId.HasValue)
                throw new TaskDomainException(TaskErrorCodes.NestingTooDeep, "Task nesting is limited to one level", 422);
            if (parent.DepartmentId != (department!.Id))
                throw new TaskDomainException(TaskErrorCodes.CrossDepartment, "Parent task must be in the same department", 422);
        }

        // TR-29: URGENT priority may only be set/cleared by ADMIN, department manager, or rank >= Lead.
        if (task.Priority != priority && (task.Priority == "URGENT" || priority == "URGENT"))
        {
            if (!await CanSetUrgentAsync(userId, task.DepartmentId))
                throw new TaskDomainException(TaskErrorCodes.AssignmentNotPermitted,
                    "Only ADMIN, the department manager, or an employee with rank >= Lead may set or clear URGENT priority", 403);
        }

        // Track changes
        var changes = new List<(string field, string? oldValue, string? newValue)>();
        var priorityChanged = task.Priority != priority;

        if (task.Title != title) changes.Add(("Title", task.Title, title));
        if (task.Description != description) changes.Add(("Description", task.Description, description));
        if (task.DepartmentId != department!.Id) changes.Add(("Department", task.DepartmentId.ToString(), department.Id.ToString()));
        if (task.AssigneeId != assigneeId)
            changes.Add(("Assignee", task.AssigneeId?.ToString() ?? "null", assigneeId?.ToString() ?? "null"));
        if (task.Priority != priority) changes.Add(("Priority", task.Priority, priority));
        if (task.DueAt != dueAt) changes.Add(("DueDate", task.DueAt?.ToString() ?? "null", dueAt?.ToString() ?? "null"));
        if (task.EstimateMinutes != estimateMinutes)
            changes.Add(("Estimate", task.EstimateMinutes?.ToString() ?? "null", estimateMinutes?.ToString() ?? "null"));
        if (task.IsArchived != isArchived) changes.Add(("Archived", task.IsArchived.ToString(), isArchived.ToString()));

        WorkTask updatedTask = null!;

        await _taskRepository.ExecuteInTransactionAsync(async () =>
        {
            if (task.AssigneeId != assigneeId && assigneeId.HasValue && assigneeId.Value != userId)
            {
                await _taskRepository.CreateNotificationAsync(
                    assigneeId.Value,
                    "TASK_ASSIGNED",
                    $"You have been assigned to task {task.Key}: {title}",
                    taskId
                );
            }

            // Update task
            task.Title = title.Trim();
            task.Description = description?.Trim();
            task.DepartmentId = department.Id;
            task.AssigneeId = assigneeId;
            task.Priority = priority;
            task.DueAt = dueAt?.ToUniversalTime();
            task.EstimateMinutes = estimateMinutes;
            task.ParentTaskId = parentTaskId;
            task.IsArchived = isArchived;
            task.UpdatedAt = DateTime.UtcNow;

            updatedTask = await _taskRepository.UpdateAsync(task);

            var correlationId = Guid.NewGuid().ToString();
            foreach (var change in changes)
            {
                await _taskRepository.AddActivityAsync(
                    taskId,
                    userId,
                    "FIELD_UPDATED",
                    change.field,
                    change.oldValue,
                    change.newValue,
                    null,
                    correlationId
                );
            }
        });

        if (priorityChanged)
        {
            await _slaService.UpdateSlaStateAsync(updatedTask);
            await _slaService.RecordSlaSnapshotAsync(updatedTask);
            updatedTask = await _taskRepository.UpdateAsync(updatedTask);
        }

        return updatedTask;
    }

    public async Task<bool> DeleteTaskAsync(int taskId, int userId)
    {
        if (!await CanDeleteTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to delete this task", 403);

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return false;

        await _taskRepository.AddActivityAsync(
            taskId,
            userId,
            "DELETED",
            null,
            null,
            null,
            null,
            Guid.NewGuid().ToString()
        );

        return await _taskRepository.DeleteAsync(taskId);
    }

    public async Task<bool> RestoreTaskAsync(int taskId, int userId)
    {
        if (!await CanDeleteTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to restore this task", 403);

        await _taskRepository.AddActivityAsync(
            taskId,
            userId,
            "RESTORED",
            null,
            null,
            null,
            null,
            Guid.NewGuid().ToString()
        );

        return await _taskRepository.RestoreAsync(taskId);
    }

    // ============================================
    // Query
    // ============================================

    public async Task<(IEnumerable<WorkTask> Items, int TotalCount)> GetTasksAsync(
        int viewerId,
        int? page = null,
        int? limit = null,
        string? search = null,
        string? status = null,
        string? priority = null,
        int? assigneeId = null,
        int? departmentId = null,
        int? createdById = null,
        bool? watchedByMe = null,
        string? tags = null,
        DateTime? dueBefore = null,
        DateTime? dueAfter = null,
        bool? overdue = null,
        bool? includeArchived = false,
        int? parentTaskId = null,
        string? sort = "-createdAt",
        bool? assigneeIsNull = null)
    {
        var user = await GetUserEmployeeAsync(viewerId);
        if (user == null)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "User not found", 400);

        if (departmentId.HasValue)
        {
            var dept = await _departmentRepository.GetByIdAsync(departmentId.Value);
            if (dept == null)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Department not found", 400);
        }

        var role = await GetUserRoleAsync(viewerId);
        var isAdmin = role == "Admin";

        // LS-1: visibility filtering happens inside GetFilteredAsync (as part of the SQL query),
        // not as a post-fetch filter, so pagination and TotalCount are both correct. The
        // assigneeId="unassigned" case is likewise pushed into the same query via assigneeIsNull.
        var result = await _taskRepository.GetFilteredAsync(
            viewerId, user.DepartmentId, isAdmin,
            page, limit, search, status, priority, assigneeId, departmentId,
            createdById, watchedByMe, tags, dueBefore, dueAfter, overdue,
            includeArchived, parentTaskId, sort, assigneeIsNull
        );

        return (result.Items, result.TotalCount);
    }

    public async Task<IEnumerable<WorkTask>> GetMyTasksAsync(int userId)
    {
        var user = await GetUserEmployeeAsync(userId);
        if (user == null)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "User not found", 400);

        var role = await GetUserRoleAsync(userId);

        // LS-6: ordered by priority severity then DueAt ascending (nulls last) - handled in repository sort.
        var result = await _taskRepository.GetFilteredAsync(
            userId, user.DepartmentId, role == "Admin",
            assigneeId: user.Id,
            status: string.Join(",", _activeStatuses),
            sort: "priority,dueAt"
        );

        return result.Items;
    }

    // ============================================
    // Status Transitions
    // ============================================

    public async Task<WorkTask> TransitionStatusAsync(int taskId, int userId, string newStatus, string? reason = null, int? assigneeId = null, string? reworkCategory = null, string? outcome = null)
    {
    var task = await _taskRepository.GetByIdAsync(taskId);
    if (task == null)
        throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

    // TR-18: allow claiming an unassigned task in the same request when moving OPEN -> IN_PROGRESS.
    var claimAssigneeId = assigneeId;
    var isClaimAttempt = claimAssigneeId.HasValue && task.Status == "OPEN" && newStatus == "IN_PROGRESS" && !task.AssigneeId.HasValue;
    if (isClaimAttempt)
    {
        if (!await CanAssignAsync(userId, claimAssigneeId!.Value, task.DepartmentId, task.AssigneeId))
            throw new TaskDomainException(TaskErrorCodes.AssignmentNotPermitted,
                "Only the department manager, ADMIN, or an employee with rank >= Lead in the same department may assign this task", 403);

        var claimAssignee = await _employeeRepository.GetByIdAsync(claimAssigneeId.Value);
        if (claimAssignee == null)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Assignee does not exist", 400);
        if (!claimAssignee.IsActive)
            throw new TaskDomainException(TaskErrorCodes.EmployeeInactive, "Assignee is not active", 422);
        if (claimAssignee.DepartmentId != task.DepartmentId)
            throw new TaskDomainException(TaskErrorCodes.CrossDepartment, "Assignee must belong to the task's department", 422);
    }

    if (!await CanTransitionTaskAsync(taskId, userId, newStatus))
        throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to perform this transition", 403);

    if (!_allowedTransitions.ContainsKey(task.Status) || !_allowedTransitions[task.Status].Contains(newStatus))
    {
        throw new TaskDomainException(TaskErrorCodes.InvalidTransition,
            $"Cannot transition task from {task.Status} to {newStatus}", 409);
    }

    if (NeedReason(task.Status, newStatus) && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5))
    {
        throw new TaskDomainException(TaskErrorCodes.ReasonRequired, "A reason of at least 5 characters is required for this transition", 422);
    }

    if (newStatus == "IN_PROGRESS" && !task.AssigneeId.HasValue && !isClaimAttempt)
    {
        throw new TaskDomainException(TaskErrorCodes.InvalidTransition, "Task must have an assignee before moving to IN_PROGRESS", 409);
    }

    // WC-18: cancellation requires outcome taxonomy at cancel time.
    if (newStatus == "CANCELLED")
    {
        if (string.IsNullOrWhiteSpace(outcome) || !ValidOutcomes.Contains(outcome))
        {
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                $"Outcome is required when cancelling; must be one of: {string.Join(", ", ValidOutcomes)}", 422);
        }
    }

    var isSelfApproval = false;

    if (newStatus == "DONE")
    {
        var blockers = await _taskRepository.GetBlockedByTasksAsync(taskId);
        var openBlockers = blockers.Where(b => b.BlockingTask.Status != "DONE" && b.BlockingTask.Status != "CANCELLED");
        if (openBlockers.Any())
            throw new TaskDomainException(TaskErrorCodes.HasOpenBlockers, "Task has open blockers that must be resolved before completion", 422);

        var checklistItems = await _taskRepository.GetChecklistItemsAsync(taskId);
        if (checklistItems.Any(ci => !ci.IsDone))
            throw new TaskDomainException(TaskErrorCodes.HasOpenBlockers, "Task has incomplete checklist items that must be resolved before completion", 422);

        var subtasks = await _taskRepository.GetSubtasksAsync(taskId);
        if (subtasks.Any(s => _activeStatuses.Contains(s.Status)))
            throw new TaskDomainException(TaskErrorCodes.HasOpenBlockers, "Task has active subtasks that must be resolved before completion", 422);

        var acceptanceCriteria = await _taskRepository.GetAcceptanceCriteriaAsync(taskId);
        if (acceptanceCriteria.Any(ac => !ac.IsMet))
        {
            var canOverride = await IsDeptManagerOrAdminAsync(userId, task.DepartmentId);
            var hasOverrideReason = !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length >= 5;
            if (!canOverride || !hasOverrideReason)
            {
                throw new TaskDomainException(TaskErrorCodes.AcceptanceCriteriaUnmet,
                    "Task has unmet acceptance criteria; only the department manager or Admin may override, and must supply a reason of at least 5 characters",
                    422);
            }
        }

        if (task.Status == "IN_REVIEW" && task.AssigneeId == userId)
            isSelfApproval = await IsSelfApprovalExempt(task);
    }

    var oldStatus = task.Status;
    WorkTask updatedTask = null!;

    // Calculate rework/reopen counts BEFORE the transaction
    int? newReworkCount = null;
    int? newReopenCount = null;

    // RW-1: IN_REVIEW -> IN_PROGRESS = Rework
    if (oldStatus == "IN_REVIEW" && newStatus == "IN_PROGRESS")
    {
        var category = ResolveReworkCategory(reason, reworkCategory);
        var reworkEvent = new ReworkEvent
        {
            TaskId = taskId,
            Category = category,
            Note = reason,
            TriggeredBy = userId,
            OccurredAt = DateTime.UtcNow,
            IsReopen = false
        };
        await _reworkRepository.CreateReworkEventAsync(reworkEvent);
        newReworkCount = task.ReworkCount + 1;
    }

    // RW-2: DONE -> OPEN = Reopen
    if (oldStatus == "DONE" && newStatus == "OPEN")
    {
        var category = ResolveReworkCategory(reason, reworkCategory);
        var reopenEvent = new ReworkEvent
        {
            TaskId = taskId,
            Category = category,
            Note = reason,
            TriggeredBy = userId,
            OccurredAt = DateTime.UtcNow,
            IsReopen = true
        };
        await _reworkRepository.CreateReworkEventAsync(reopenEvent);
        newReopenCount = task.ReopenCount + 1;
    }

    await _taskRepository.ExecuteInTransactionAsync(async () =>
    {
        var now = DateTime.UtcNow;

        if (isClaimAttempt)
        {
            task.AssigneeId = claimAssigneeId;
            task.UpdatedAt = now;
            await _taskRepository.UpdateAsync(task);
            await _taskRepository.AddWatcherAsync(taskId, claimAssigneeId!.Value);
            await _taskRepository.AddActivityAsync(
                taskId, userId, "ASSIGNED", "Assignee", "null", claimAssigneeId.Value.ToString(), null, Guid.NewGuid().ToString());
        }

        // Update status with rework/reopen counts
        updatedTask = await _taskRepository.UpdateStatusAsync(taskId, newStatus, reason, newReworkCount, newReopenCount);

        if (newStatus == "CANCELLED")
        {
            updatedTask.Outcome = outcome!;
            await _taskRepository.UpdateAsync(updatedTask);

            await _closureRepository.CreateOutcomeAsync(new TaskOutcome
            {
                TaskId = taskId,
                Outcome = outcome!,
                Note = reason,
                OccurredAt = now
            });

            await _taskRepository.AddActivityAsync(
                taskId, userId, "OUTCOME_SET", "Outcome", null, outcome, reason, Guid.NewGuid().ToString());
        }

        // WC-23: close the interval for the old status and open one for the new status
        await _taskRepository.CloseOpenStatusIntervalAsync(taskId, now);
        await _taskRepository.OpenStatusIntervalAsync(taskId, newStatus, userId, now);

        await _taskRepository.AddActivityAsync(
            taskId,
            userId,
            isSelfApproval ? "SELF_APPROVED" : "STATUS_CHANGED",
            "Status",
            oldStatus,
            newStatus,
            reason,
            Guid.NewGuid().ToString()
        );

        await NotifyWatchersAsync(taskId, userId, $"Status changed from {oldStatus} to {newStatus}");
    });

    return updatedTask;
    }
    public async Task<List<string>> GetAvailableTransitionsAsync(int taskId, int userId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            return new List<string>();

        if (!_allowedTransitions.ContainsKey(task.Status))
            return new List<string>();

        var transitions = new List<string>();
        foreach (var status in _allowedTransitions[task.Status])
        {
            if (await CanTransitionTaskAsync(taskId, userId, status))
                transitions.Add(status);
        }

        return transitions;
    }

    private bool NeedReason(string fromStatus, string toStatus)
    {
        var needReason = new HashSet<(string, string)>
        {
            ("OPEN", "CANCELLED"),
            ("IN_PROGRESS", "BLOCKED"),
            ("BLOCKED", "CANCELLED"),
            ("DONE", "OPEN"),
            ("CANCELLED", "OPEN"),
            ("IN_REVIEW", "IN_PROGRESS") // "rejection back to assignee" - SRS requires a comment/reason.
        };
        return needReason.Contains((fromStatus, toStatus));
    }

    // SRS 6.4: an assignee who is also the creator may self-approve IN_REVIEW -> DONE only when
    // nobody else in their department is currently active (i.e. there's no one else who could review it).
    private async Task<bool> IsSelfApprovalExempt(WorkTask task)
    {
        if (!task.AssigneeId.HasValue || task.CreatedById != task.AssigneeId.Value)
            return false;

        var deptEmployees = await _employeeRepository.GetByDepartmentIdAsync(task.DepartmentId);
        var otherActiveMembers = deptEmployees.Where(e => e.IsActive && e.Id != task.AssigneeId.Value);
        return !otherActiveMembers.Any();
    }

    // ============================================
    // Assignment
    // ============================================

    public async Task<WorkTask> AssignTaskAsync(int taskId, int userId, int? assigneeId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

        if (!await CanEditTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to assign this task", 403);

        WorkTask resultTask = task;

        if (!assigneeId.HasValue)
        {
            var oldAssignee = task.AssigneeId;
            var wasInProgress = task.Status == "IN_PROGRESS";

            await _taskRepository.ExecuteInTransactionAsync(async () =>
            {
                task.AssigneeId = null;
                // TR-26: releasing the assignee from an IN_PROGRESS task moves it back to OPEN.
                if (wasInProgress)
                    task.Status = "OPEN";
                task.UpdatedAt = DateTime.UtcNow;

                await _taskRepository.UpdateAsync(task);

                await _taskRepository.AddActivityAsync(
                    taskId,
                    userId,
                    "UNASSIGNED",
                    "Assignee",
                    oldAssignee?.ToString() ?? "null",
                    "null",
                    null,
                    Guid.NewGuid().ToString()
                );

                if (wasInProgress)
                {
                    await _taskRepository.AddActivityAsync(
                        taskId, userId, "STATUS_CHANGED", "Status", "IN_PROGRESS", "OPEN", null, Guid.NewGuid().ToString());
                }
            });

            return task;
        }

        var assignee = await _employeeRepository.GetByIdAsync(assigneeId.Value);
        if (assignee == null)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Assignee does not exist", 400);
        if (!assignee.IsActive)
            throw new TaskDomainException(TaskErrorCodes.EmployeeInactive, "Assignee is not active", 422);

        var userRole = await GetUserRoleAsync(userId);
        if (assignee.DepartmentId != task.DepartmentId && userRole != "Admin")
            throw new TaskDomainException(TaskErrorCodes.CrossDepartment, "Assignee must belong to the task's department", 422);

        if (!await CanAssignAsync(userId, assigneeId.Value, task.DepartmentId, task.AssigneeId))
            throw new TaskDomainException(TaskErrorCodes.AssignmentNotPermitted,
                "Only the department manager, ADMIN, or an employee with rank >= Lead in the same department may assign this task", 403);

        var oldAssigneeId = task.AssigneeId;
        WorkTask updatedTask = null!;

        await _taskRepository.ExecuteInTransactionAsync(async () =>
        {
            task.AssigneeId = assigneeId;
            task.UpdatedAt = DateTime.UtcNow;
            updatedTask = await _taskRepository.UpdateAsync(task);

            await _taskRepository.AddActivityAsync(
                taskId,
                userId,
                "ASSIGNED",
                "Assignee",
                oldAssigneeId?.ToString() ?? "null",
                assigneeId.ToString(),
                null,
                Guid.NewGuid().ToString()
            );

            await _taskRepository.AddWatcherAsync(taskId, assigneeId.Value);

            // TR-27: on reassignment, notify BOTH the previous assignee and the new one.
            if (oldAssigneeId.HasValue && oldAssigneeId.Value != assigneeId.Value && oldAssigneeId.Value != userId)
            {
                await _taskRepository.CreateNotificationAsync(
                    oldAssigneeId.Value,
                    "TASK_UNASSIGNED",
                    $"You have been unassigned from task {task.Key}: {task.Title}",
                    taskId
                );
            }

            if (assigneeId.Value != userId)
            {
                await _taskRepository.CreateNotificationAsync(
                    assigneeId.Value,
                    "TASK_ASSIGNED",
                    $"You have been assigned to task {task.Key}: {task.Title}",
                    taskId
                );
            }
        });

        return updatedTask;
    }

    public async Task<bool> CanAssignAsync(int userId, int? targetEmployeeId, int departmentId, int? currentAssigneeId = null)
    {
        var user = await GetUserEmployeeAsync(userId);
        if (user == null) return false;

        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        if (!targetEmployeeId.HasValue) return true;

        var target = await _employeeRepository.GetByIdAsync(targetEmployeeId.Value);
        if (target == null) return false;

        if (target.DepartmentId != departmentId) return false;

        // MEMBER "claim unassigned only" nuance (7.1): self-assignment is only automatically allowed
        // when the task is currently unassigned or the actor already IS the assignee (idempotent).
        // A member can't use "assigning to self" to grab a task someone else currently holds.
        if (userId == targetEmployeeId.Value && (!currentAssigneeId.HasValue || currentAssigneeId.Value == userId))
            return true;

        if (user.DepartmentId == departmentId)
        {
            var dept = await _departmentRepository.GetByIdAsync(departmentId);
            if (dept?.ManagerId == userId) return true;
        }

        // No real MANAGER role exists yet, so the "manager may assign to reports" rule is approximated
        // as: any user may assign within their FULL reporting subtree (not just direct reports).
        if (await IsInReportingSubtreeAsync(userId, targetEmployeeId.Value))
            return true;

        var userLevel = user.LevelId;
        var targetLevel = target.LevelId;

        var levels = await _levelRepository.GetAllAsync();
        var userLevelObj = levels.FirstOrDefault(l => l.Id == userLevel);
        var targetLevelObj = levels.FirstOrDefault(l => l.Id == targetLevel);

        var leadLevel = levels.FirstOrDefault(l => l.Name.Equals("Lead", StringComparison.OrdinalIgnoreCase));
        var leadRank = leadLevel?.Rank ?? 50;

        if (userLevelObj != null && targetLevelObj != null && userLevelObj.Rank >= leadRank)
        {
            return targetLevelObj.Rank < userLevelObj.Rank && target.DepartmentId == user.DepartmentId;
        }

        return false;
    }

    // Walks the full reporting subtree below managerId (not just direct reports) via BFS.
    private async Task<bool> IsInReportingSubtreeAsync(int managerId, int targetEmployeeId)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(managerId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            var directReports = await _employeeRepository.GetDirectReportsAsync(current);
            foreach (var report in directReports)
            {
                if (report.Id == targetEmployeeId) return true;
                queue.Enqueue(report.Id);
            }
        }

        return false;
    }

    // TR-29: dynamically looks up the "Lead" seniority level's rank rather than hardcoding it.
    private async Task<bool> CanSetUrgentAsync(int userId, int departmentId)
    {
        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        var dept = await _departmentRepository.GetByIdAsync(departmentId);
        if (dept?.ManagerId == userId) return true;

        var user = await GetUserEmployeeAsync(userId);
        if (user == null) return false;

        var levels = await _levelRepository.GetAllAsync();
        var leadLevel = levels.FirstOrDefault(l => l.Name.Equals("Lead", StringComparison.OrdinalIgnoreCase));
        var leadRank = leadLevel?.Rank ?? 50;

        var userLevelObj = levels.FirstOrDefault(l => l.Id == user.LevelId);
        return userLevelObj != null && userLevelObj.Rank >= leadRank;
    }

    // TR-28: employee-deactivation-aware helpers, wired up by a later EmployeesController pass.
    public async Task<int> CountActiveTasksForEmployeeAsync(int employeeId)
    {
        var result = await _taskRepository.GetFilteredAsync(
            employeeId, null, true,
            assigneeId: employeeId,
            status: string.Join(",", _activeStatuses),
            includeArchived: true);

        return result.TotalCount;
    }

    public async Task ReassignAllActiveTasksAsync(int fromEmployeeId, int toEmployeeId, int actorUserId)
    {
        var toEmployee = await _employeeRepository.GetByIdAsync(toEmployeeId);
        if (toEmployee == null)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Target employee does not exist", 400);
        if (!toEmployee.IsActive)
            throw new TaskDomainException(TaskErrorCodes.EmployeeInactive, "Target employee is not active", 422);

        var result = await _taskRepository.GetFilteredAsync(
            actorUserId, null, true,
            assigneeId: fromEmployeeId,
            status: string.Join(",", _activeStatuses),
            includeArchived: true);

        await _taskRepository.ExecuteInTransactionAsync(async () =>
        {
            foreach (var task in result.Items)
            {
                var oldAssigneeId = task.AssigneeId;
                task.AssigneeId = toEmployeeId;
                task.UpdatedAt = DateTime.UtcNow;
                await _taskRepository.UpdateAsync(task);

                await _taskRepository.AddActivityAsync(
                    task.Id, actorUserId, "ASSIGNED", "Assignee",
                    oldAssigneeId?.ToString() ?? "null", toEmployeeId.ToString(), null, Guid.NewGuid().ToString());

                await _taskRepository.AddWatcherAsync(task.Id, toEmployeeId);

                if (toEmployeeId != actorUserId)
                {
                    await _taskRepository.CreateNotificationAsync(
                        toEmployeeId, "TASK_ASSIGNED",
                        $"You have been assigned to task {task.Key}: {task.Title}", task.Id);
                }
            }
        });
    }

    // ============================================
    // Comments
    // ============================================

    public async Task<TaskComment> AddCommentAsync(int taskId, int authorId, string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Comment must be between 1 and 2000 characters", 400);

        if (!await CanReadTaskAsync(taskId, authorId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to comment on this task", 403);

        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorId = authorId,
            Body = body.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var addedComment = await _taskRepository.AddCommentAsync(comment);

        await _taskRepository.AddActivityAsync(
            taskId,
            authorId,
            "COMMENTED",
            null,
            null,
            null,
            null,
            Guid.NewGuid().ToString()
        );

        await NotifyWatchersAsync(taskId, authorId, $"New comment: {body.Trim().Substring(0, Math.Min(50, body.Length))}...", "COMMENT_ADDED");

        return addedComment;
    }

    public async Task<TaskComment> UpdateCommentAsync(int commentId, int userId, string body)
    {
        var comment = await _taskRepository.GetCommentByIdAsync(commentId);
        if (comment == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Comment not found", 404);
        if (comment.AuthorId != userId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You can only edit your own comments", 403);

        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Comment must be between 1 and 2000 characters", 400);

        comment.Body = body.Trim();
        comment.UpdatedAt = DateTime.UtcNow;

        await _taskRepository.UpdateCommentAsync(comment);
        return comment;
    }

    public async Task<bool> DeleteCommentAsync(int commentId, int userId)
    {
        var comment = await _taskRepository.GetCommentByIdAsync(commentId);
        if (comment == null) return false;
        if (comment.AuthorId != userId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You can only delete your own comments", 403);

        return await _taskRepository.DeleteCommentAsync(commentId);
    }

    public async Task<IEnumerable<TaskComment>> GetCommentsAsync(int taskId, int viewerId, int? page = null, int? limit = null)
    {
        if (!await CanReadTaskAsync(taskId, viewerId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view comments", 403);

        return await _taskRepository.GetCommentsByTaskIdAsync(taskId, page, limit);
    }

    // ============================================
    // Checklist
    // ============================================

    public async Task<TaskChecklistItem> AddChecklistItemAsync(int taskId, int userId, string label, int position)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length > 200)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Checklist label must be between 1 and 200 characters", 400);

        if (!await CanEditTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to edit this task", 403);

        return await _taskRepository.AddChecklistItemAsync(taskId, label, position);
    }

    public async Task<TaskChecklistItem> UpdateChecklistItemAsync(int itemId, int userId, string? label, bool? isDone, int? position)
    {
        var item = await _taskRepository.GetChecklistItemByIdAsync(itemId);
        if (item == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Checklist item not found", 404);

        if (!await CanEditTaskAsync(item.TaskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to edit this task", 403);

        // TR-40: record who completed the item, not just when.
        return await _taskRepository.UpdateChecklistItemAsync(itemId, label, isDone, position, completedById: userId);
    }

    public async Task<bool> DeleteChecklistItemAsync(int itemId, int userId)
    {
        var item = await _taskRepository.GetChecklistItemByIdAsync(itemId);
        if (item == null) return false;

        if (!await CanEditTaskAsync(item.TaskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to edit this task", 403);

        return await _taskRepository.DeleteChecklistItemAsync(itemId);
    }

    public async Task<IEnumerable<TaskChecklistItem>> GetChecklistItemsAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetChecklistItemsAsync(taskId);
    }

    // ============================================
    // Dependencies
    // ============================================

    public async Task<TaskDependency> AddDependencyAsync(int blockedTaskId, int blockingTaskId, int userId)
    {
        if (blockedTaskId == blockingTaskId)
            throw new TaskDomainException(TaskErrorCodes.CircularDependency, "A task cannot block itself", 422);

        if (!await CanEditTaskAsync(blockedTaskId, userId) || !await CanEditTaskAsync(blockingTaskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to add dependencies", 403);

        if (await WouldCreateCycle(blockedTaskId, blockingTaskId))
            throw new TaskDomainException(TaskErrorCodes.CircularDependency, "This would create a circular dependency", 422);

        return await _taskRepository.AddDependencyAsync(blockedTaskId, blockingTaskId);
    }

    public async Task<bool> RemoveDependencyAsync(int blockedTaskId, int blockingTaskId, int userId)
    {
        if (!await CanEditTaskAsync(blockedTaskId, userId) || !await CanEditTaskAsync(blockingTaskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to remove dependencies", 403);

        return await _taskRepository.RemoveDependencyAsync(blockedTaskId, blockingTaskId);
    }

    public async Task<IEnumerable<TaskDependency>> GetBlockedByAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetBlockedByTasksAsync(taskId);
    }

    public async Task<IEnumerable<TaskDependency>> GetBlockingAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetBlockingTasksAsync(taskId);
    }

    private async Task<bool> WouldCreateCycle(int taskId, int potentialBlockingTaskId)
    {
        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(potentialBlockingTaskId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (visited.Contains(current)) continue;
            visited.Add(current);

            if (current == taskId) return true;

            var blockers = await _taskRepository.GetBlockedByTasksAsync(current);
            foreach (var blocker in blockers)
            {
                stack.Push(blocker.BlockingTaskId);
            }
        }

        return false;
    }

    // ============================================
    // Time Entries
    // ============================================

    public async Task<TaskTimeEntry> AddTimeEntryAsync(int taskId, int userId, int minutes, string? note, DateTime workedOn)
    {
        if (minutes < 1 || minutes > 1440)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Minutes must be between 1 and 1440", 400);

        // A missing workedOn binds to default(DateTime) (0001-01-01), which passed the
        // not-in-the-future check below and was silently persisted as a valid entry - reject it
        // explicitly instead of guessing at a default.
        if (workedOn == default)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "workedOn is required", 400);

        if (workedOn > DateTime.UtcNow)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Worked on date cannot be in the future", 400);

        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to log time on this task", 403);

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

        if (task.AssigneeId != userId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You can only log time on tasks assigned to you", 403);

        // A caller sending a date-only value (e.g. "2026-09-03") binds to DateTimeKind.Unspecified;
        // treat that as UTC-midnight rather than calling ToUniversalTime() (which would wrongly
        // shift it by the server's local offset). Npgsql refuses to persist Unspecified into a
        // timestamptz column, so this normalization must happen before it reaches the repository,
        // not just for the local workedDate calculation below.
        var workedOnUtc = workedOn.Kind == DateTimeKind.Utc
            ? workedOn
            : DateTime.SpecifyKind(workedOn, DateTimeKind.Utc);
        var workedDate = DateOnly.FromDateTime(workedOnUtc);
        await _timesheetService.EnsureTimeEntryEditableAsync(userId, workedDate);

        var entry = await _taskRepository.AddTimeEntryAsync(taskId, userId, minutes, note, workedOnUtc);
        await _timesheetService.LinkEntryToWeekAsync(userId, entry.Id, workedDate);

        var timeEntries = await _taskRepository.GetTimeEntriesAsync(taskId);
        task.LoggedMinutes = timeEntries.Sum(te => te.Minutes);
        await _taskRepository.UpdateAsync(task);

        return entry;
    }

    public async Task<IEnumerable<TaskTimeEntry>> GetTimeEntriesAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetTimeEntriesAsync(taskId);
    }

    public async Task<(IEnumerable<TaskTimeEntry> Items, string? NextCursor)> GetTimeEntriesCursorAsync(
        int taskId, int userId, string? cursor, int limit)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetTimeEntriesCursorAsync(taskId, cursor, limit);
    }

    public async Task<bool> DeleteTimeEntryAsync(int timeEntryId, int userId)
    {
        // TR-44 + CP-15: ownership scoped in repository; approved-week immutability enforced there.
        return await _taskRepository.DeleteTimeEntryAsync(timeEntryId, userId);
    }

    // ============================================
    // Watchers
    // ============================================

    public async Task<TaskWatcher> AddWatcherAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to watch this task", 403);

        var existing = await _taskRepository.IsWatchingAsync(taskId, userId);
        if (existing)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "You are already watching this task", 400);

        return await _taskRepository.AddWatcherAsync(taskId, userId);
    }

    public async Task<bool> RemoveWatcherAsync(int taskId, int userId)
    {
        return await _taskRepository.RemoveWatcherAsync(taskId, userId);
    }

    public async Task<IEnumerable<TaskWatcher>> GetWatchersAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetWatchersAsync(taskId);
    }

    public async Task<bool> IsWatchingAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            return false;

        return await _taskRepository.IsWatchingAsync(taskId, userId);
    }

    // WT routes: watch/unwatch self OR (if permitted) another employee.
    // Watching another employee on their behalf is treated as an assignment-adjacent action, so it
    // reuses CanAssignAsync as a reasonable proxy permission check (SRS: "self, or another employee
    // if permitted" without specifying an exact rule).
    public async Task<TaskWatcher> AddWatcherForAsync(int taskId, int actorId, int targetEmployeeId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

        if (targetEmployeeId == actorId)
        {
            if (!await CanReadTaskAsync(taskId, actorId))
                throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to watch this task", 403);
        }
        else
        {
            if (!await CanAssignAsync(actorId, targetEmployeeId, task.DepartmentId, task.AssigneeId))
                throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to add this employee as a watcher", 403);
        }

        if (await _taskRepository.IsWatchingAsync(taskId, targetEmployeeId))
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "This employee is already watching the task", 400);

        return await _taskRepository.AddWatcherAsync(taskId, targetEmployeeId);
    }

    public async Task<bool> RemoveWatcherForAsync(int taskId, int actorId, int targetEmployeeId)
    {
        if (targetEmployeeId != actorId && !await CanEditTaskAsync(taskId, actorId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to remove this watcher", 403);

        return await _taskRepository.RemoveWatcherAsync(taskId, targetEmployeeId);
    }

    // ============================================
    // Tags
    // ============================================

    public async Task<IEnumerable<Tag>> GetTagsAsync()
    {
        return await _taskRepository.GetAllTagsAsync();
    }

    public async Task<Tag> CreateTagAsync(string name, int userId)
    {
        var userRole = await GetUserRoleAsync(userId);
        if (userRole != "Admin")
            throw new TaskDomainException(TaskErrorCodes.TagCreationNotPermitted, "Only admins can create tags", 403);

        return await _taskRepository.GetOrCreateTagAsync(name);
    }

    public async Task<bool> DeleteTagAsync(int tagId, int userId)
    {
        var userRole = await GetUserRoleAsync(userId);
        if (userRole != "Admin")
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Only admins can delete tags", 403);

        return await _taskRepository.DeleteTagAsync(tagId);
    }

    public async Task<IEnumerable<Tag>> GetTagsForTaskAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetTagsForTaskAsync(taskId);
    }

    public async Task<List<(Tag Tag, int Count)>> GetTagsWithUsageCountsAsync()
    {
        return await _taskRepository.GetTagsWithUsageCountsAsync();
    }

    // PUT /v1/tasks/:id/tags - atomically replaces the task's full tag set.
    public async Task<IEnumerable<Tag>> ReplaceTaskTagsAsync(int taskId, int userId, List<string> tagNames)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

        if (!await CanEditTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to edit this task", 403);

        var distinctNames = (tagNames ?? new List<string>())
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctNames.Count > 10)
            throw new TaskDomainException(TaskErrorCodes.LimitExceeded, "A task may have at most 10 tags", 422);

        // TR-41: only ADMIN may create brand-new tags; anyone who can edit the task may attach existing ones.
        var actorRole = await GetUserRoleAsync(userId);
        var resolvedTags = new List<Tag>();
        foreach (var name in distinctNames)
        {
            var existing = await _taskRepository.FindTagByNameAsync(name);
            if (existing == null && actorRole != "Admin")
                throw new TaskDomainException(TaskErrorCodes.TagCreationNotPermitted,
                    $"You do not have permission to create a new tag ('{name}')", 403);

            var tag = existing ?? await _taskRepository.GetOrCreateTagAsync(name);
            resolvedTags.Add(tag);
        }

        var currentTags = (await _taskRepository.GetTagsForTaskAsync(taskId)).ToList();
        var toRemove = currentTags.Where(ct => !resolvedTags.Any(rt => rt.Id == ct.Id)).ToList();
        var toAdd = resolvedTags.Where(rt => !currentTags.Any(ct => ct.Id == rt.Id)).ToList();

        await _taskRepository.ExecuteInTransactionAsync(async () =>
        {
            foreach (var t in toRemove)
                await _taskRepository.RemoveTagFromTaskAsync(taskId, t.Id);
            foreach (var t in toAdd)
                await _taskRepository.AddTagToTaskAsync(taskId, t.Id);
        });

        return resolvedTags;
    }

    // ============================================
    // Notifications
    // ============================================

    public async Task<IEnumerable<Notification>> GetMyNotificationsAsync(int userId, bool? unreadOnly = null, int? page = null, int? limit = null)
    {
        return await _taskRepository.GetNotificationsAsync(userId, unreadOnly, page, limit);
    }

    public async Task<(IEnumerable<Notification> Items, string? NextCursor)> GetMyNotificationsCursorAsync(
        int userId, bool? unreadOnly, string? cursor, int limit)
    {
        return await _taskRepository.GetNotificationsCursorAsync(userId, unreadOnly, cursor, limit);
    }

    public async Task<int> GetUnreadNotificationCountAsync(int userId)
    {
        return await _taskRepository.GetUnreadCountAsync(userId);
    }

    public async Task<bool> MarkNotificationReadAsync(int notificationId, int userId)
    {
        return await _taskRepository.MarkNotificationReadAsync(notificationId, userId);
    }

    public async Task<bool> MarkAllNotificationsReadAsync(int userId)
    {
        return await _taskRepository.MarkAllNotificationsReadAsync(userId);
    }

    // ============================================
    // Permissions
    // ============================================

    public async Task<bool> CanReadTaskAsync(int taskId, int userId)
    {
        var user = await GetUserEmployeeAsync(userId);
        if (user == null) return false;

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return false;

        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        if (task.DepartmentId == user.DepartmentId) return true;
        if (task.CreatedById == userId) return true;
        if (task.AssigneeId == userId) return true;

        var directReports = await _employeeRepository.GetDirectReportsAsync(userId);
        if (directReports.Any(e => e.Id == task.AssigneeId)) return true;

        return false;
    }

    public async Task<bool> CanEditTaskAsync(int taskId, int userId)
    {
        var user = await GetUserEmployeeAsync(userId);
        if (user == null) return false;

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return false;

        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        if (task.CreatedById == userId) return true;
        if (task.AssigneeId == userId) return true;

        var dept = await _departmentRepository.GetByIdAsync(task.DepartmentId);
        if (dept?.ManagerId == userId) return true;

        var directReports = await _employeeRepository.GetDirectReportsAsync(userId);
        if (directReports.Any(e => e.Id == task.AssigneeId)) return true;

        return false;
    }

    public async Task<bool> CanDeleteTaskAsync(int taskId, int userId)
    {
        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return false;

        if (task.CreatedById == userId) return true;

        var dept = await _departmentRepository.GetByIdAsync(task.DepartmentId);
        if (dept?.ManagerId == userId) return true;

        return false;
    }

    // Who may perform each specific transition (SRS 6.2) - deliberately NOT a blanket "creator/
    // assignee can always transition" rule, since several rows restrict to only one of them
    // (e.g. BLOCKED->CANCELLED is creator-only, IN_PROGRESS->OPEN is assignee-only).
    public async Task<bool> CanTransitionTaskAsync(int taskId, int userId, string newStatus)
    {
        var user = await GetUserEmployeeAsync(userId);
        if (user == null) return false;

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return false;

        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        var dept = await _departmentRepository.GetByIdAsync(task.DepartmentId);
        if (dept?.ManagerId == userId) return true;

        var isAssignee = task.AssigneeId == userId;
        var isCreator = task.CreatedById == userId;

        switch (task.Status, newStatus)
        {
            case ("OPEN", "IN_PROGRESS"):
                return isAssignee || isCreator;
            case ("OPEN", "CANCELLED"):
                return isCreator;
            case ("IN_PROGRESS", "IN_REVIEW"):
            case ("IN_PROGRESS", "BLOCKED"):
            case ("IN_PROGRESS", "OPEN"):
            case ("BLOCKED", "IN_PROGRESS"):
                return isAssignee;
            case ("BLOCKED", "CANCELLED"):
                return isCreator;
            case ("IN_REVIEW", "DONE"):
                // "Anyone permitted to review" - not the assignee (unless self-approval exempt),
                // scoped to the task's own department per the Section 8 permission matrix.
                if (isAssignee) return await IsSelfApprovalExempt(task);
                return user.DepartmentId == task.DepartmentId;
            case ("IN_REVIEW", "IN_PROGRESS"):
                // Reviewer only - the assignee cannot reject their own submission back to themselves.
                if (isAssignee) return false;
                return user.DepartmentId == task.DepartmentId;
            case ("DONE", "OPEN"):
                return isCreator;
            case ("CANCELLED", "OPEN"):
                return isCreator;
            default:
                return false;
        }
    }

    public async Task<Employee?> GetUserEmployeeAsync(int userId)
    {
        return await _employeeRepository.GetByIdAsync(userId);
    }

    // ============================================
    // Activity
    // ============================================

    public async Task<IEnumerable<TaskActivity>> GetTaskActivityAsync(int taskId, int userId, int? page = null, int? limit = null)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetActivitiesAsync(taskId, page, limit);
    }

    public async Task<(IEnumerable<TaskActivity> Items, string? NextCursor)> GetTaskActivityCursorAsync(
        int taskId, int userId, string? cursor, int limit)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetActivitiesCursorAsync(taskId, cursor, limit);
    }

    // ============================================
    // Archive
    // ============================================

    public async Task<WorkTask> SetArchivedAsync(int taskId, int userId, bool isArchived)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

        if (!await CanEditTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to edit this task", 403);

        if (task.IsArchived == isArchived)
            return task;

        var oldValue = task.IsArchived.ToString();
        task.IsArchived = isArchived;
        task.UpdatedAt = DateTime.UtcNow;
        var updated = await _taskRepository.UpdateAsync(task);

        await _taskRepository.AddActivityAsync(
            taskId, userId, "FIELD_UPDATED", "Archived", oldValue, isArchived.ToString(), null, Guid.NewGuid().ToString());

        return updated;
    }

    // ============================================
    // Department Reporting (RP-1, RP-2)
    // ============================================

    public async Task<(Dictionary<string, int> StatusCounts, int Overdue, int Unassigned)> GetDepartmentTaskSummaryAsync(int departmentId)
    {
        return await _taskRepository.GetDepartmentTaskSummaryAsync(departmentId);
    }

    public async Task<List<(int EmployeeId, string EmployeeName, Dictionary<string, int> PriorityCounts)>> GetDepartmentWorkloadAsync(int departmentId)
    {
        return await _taskRepository.GetDepartmentWorkloadAsync(departmentId);
    }

    // NOTE: throughout TaskService, "userId" parameters are actually Employee.Id (the controller resolves
    // the JWT's User -> User.EmployeeId before calling in). Role lives on User, so we look it up via the
    // reverse link (User.EmployeeId == employeeId) rather than treating employeeId as a User.Id.
    private async Task<string> GetUserRoleAsync(int employeeId)
    {
        var user = await _userRepository.GetByEmployeeIdAsync(employeeId);
        return user?.Role ?? "Member";
    }

    private async Task NotifyWatchersAsync(int taskId, int actorId, string message, string notificationType = "STATUS_CHANGED")
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return;

        var watchers = await _taskRepository.GetWatchersAsync(taskId);
        var recipientIds = watchers
            .Select(w => w.EmployeeId)
            .Where(id => id != actorId)
            .ToHashSet();

        // SRS §9.4: status/comment alerts go to the assignee as well as watchers.
        if (task.AssigneeId.HasValue && task.AssigneeId.Value != actorId)
            recipientIds.Add(task.AssigneeId.Value);

        // Creators are implicit watchers (WT-2), but they should not be flooded with every
        // status transition the assignee makes on work they only created. They still get
        // comment/mention-style alerts (non-STATUS_CHANGED) and remain watchers for UI.
        if (notificationType == "STATUS_CHANGED"
            && task.CreatedById != actorId
            && task.AssigneeId != task.CreatedById)
        {
            recipientIds.Remove(task.CreatedById);
        }

        foreach (var recipientId in recipientIds)
        {
            await _taskRepository.CreateNotificationAsync(
                recipientId,
                notificationType,
                $"{message} on task {task.Key}: {task.Title}",
                taskId
            );
        }
    }

    // WC-13: shared "may act as reviewer/approver authority" check - department manager or Admin.
    private async Task<bool> IsDeptManagerOrAdminAsync(int userId, int departmentId)
    {
        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        var dept = await _departmentRepository.GetByIdAsync(departmentId);
        return dept?.ManagerId == userId;
    }

    // ============================================
    // Part B - Status Duration Tracking (WC-23..WC-27)
    // ============================================

    public async Task<IEnumerable<TaskStatusInterval>> GetStatusIntervalsAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetStatusIntervalsAsync(taskId);
    }

    // ============================================
    // Part B - Acceptance Criteria / Definition of Done (WC-10..WC-14)
    // ============================================

    // WC-11: creator, department manager, or Admin only - deliberately NOT the same rule as
    // CanEditTaskAsync (which lets the assignee edit general fields; acceptance criteria are stricter).
    public async Task<bool> CanManageAcceptanceCriteriaAsync(int taskId, int userId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return false;

        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        if (task.CreatedById == userId) return true;

        var dept = await _departmentRepository.GetByIdAsync(task.DepartmentId);
        return dept?.ManagerId == userId;
    }

    public async Task<TaskAcceptanceCriterion> AddAcceptanceCriterionAsync(int taskId, int userId, string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Acceptance criterion text must be between 1 and 200 characters", 400);

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Task not found", 404);

        if (!await CanManageAcceptanceCriteriaAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden,
                "Only the task creator, department manager, or Admin may manage acceptance criteria", 403);

        var existing = await _taskRepository.GetAcceptanceCriteriaAsync(taskId);
        var position = existing.Count();

        var criterion = await _taskRepository.AddAcceptanceCriterionAsync(taskId, text, position);

        await _taskRepository.AddActivityAsync(
            taskId, userId, "ACCEPTANCE_CRITERION_ADDED", null, null, text, null, Guid.NewGuid().ToString());

        return criterion;
    }

    public async Task<TaskAcceptanceCriterion> UpdateAcceptanceCriterionAsync(int criterionId, int userId, string? text, bool? isMet)
    {
        var criterion = await _taskRepository.GetAcceptanceCriterionByIdAsync(criterionId);
        if (criterion == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Acceptance criterion not found", 404);

        if (!await CanManageAcceptanceCriteriaAsync(criterion.TaskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden,
                "Only the task creator, department manager, or Admin may manage acceptance criteria", 403);

        string? trimmedText = null;
        if (text != null)
        {
            trimmedText = text.Trim();
            if (string.IsNullOrWhiteSpace(trimmedText) || trimmedText.Length > 200)
                throw new TaskDomainException(TaskErrorCodes.ValidationError, "Acceptance criterion text must be between 1 and 200 characters", 400);
        }

        var wasMet = criterion.IsMet;
        var task = await _taskRepository.GetByIdAsync(criterion.TaskId);

        var updated = await _taskRepository.UpdateAcceptanceCriterionAsync(criterionId, trimmedText, isMet, userId);

        if (isMet.HasValue && isMet.Value != wasMet)
        {
            // WC-14: record marking/unmarking in the task's activity log.
            await _taskRepository.AddActivityAsync(
                criterion.TaskId, userId,
                isMet.Value ? "ACCEPTANCE_CRITERION_MET" : "ACCEPTANCE_CRITERION_UNMET",
                "AcceptanceCriterion", wasMet.ToString(), isMet.Value.ToString(), null, Guid.NewGuid().ToString());

            // WC-14: unmarking an already-met criterion after the task has entered IN_REVIEW (or later)
            // notifies "the reviewer". There's no single well-defined reviewer role/field in this
            // codebase, so this approximates it as the task's current watchers (excluding the actor).
            var reviewOrLaterStatuses = new[] { "IN_REVIEW", "DONE" };
            if (!isMet.Value && task != null && reviewOrLaterStatuses.Contains(task.Status))
            {
                await NotifyWatchersAsync(criterion.TaskId, userId,
                    $"Acceptance criterion unmarked as met: \"{updated.Text}\"",
                    "ACCEPTANCE_CRITERION_UNMET");
            }
        }

        return updated;
    }

    public async Task<bool> DeleteAcceptanceCriterionAsync(int criterionId, int userId)
    {
        var criterion = await _taskRepository.GetAcceptanceCriterionByIdAsync(criterionId);
        if (criterion == null) return false;

        if (!await CanManageAcceptanceCriteriaAsync(criterion.TaskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden,
                "Only the task creator, department manager, or Admin may manage acceptance criteria", 403);

        return await _taskRepository.DeleteAcceptanceCriterionAsync(criterionId);
    }

    public async Task<IEnumerable<TaskAcceptanceCriterion>> GetAcceptanceCriteriaAsync(int taskId, int userId)
    {
        if (!await CanReadTaskAsync(taskId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "You don't have permission to view this task", 403);

        return await _taskRepository.GetAcceptanceCriteriaAsync(taskId);
    }

    // WC-12: department default Definition of Done - Admin/dept-manager only to manage.
    private async Task<bool> CanManageDepartmentDefaultCriteriaAsync(int departmentId, int userId)
    {
        var userRole = await GetUserRoleAsync(userId);
        if (userRole == "Admin") return true;

        var dept = await _departmentRepository.GetByIdAsync(departmentId);
        return dept?.ManagerId == userId;
    }

    public async Task<DepartmentDefaultCriterion> AddDepartmentDefaultCriterionAsync(int departmentId, int userId, string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Default criterion text must be between 1 and 200 characters", 400);

        var dept = await _departmentRepository.GetByIdAsync(departmentId);
        if (dept == null)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Department does not exist", 400);

        if (!await CanManageDepartmentDefaultCriteriaAsync(departmentId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Only the department manager or Admin may manage default criteria", 403);

        var existing = await _taskRepository.GetDepartmentDefaultCriteriaAsync(departmentId);
        var position = existing.Count();

        return await _taskRepository.AddDepartmentDefaultCriterionAsync(departmentId, text, position);
    }

    public async Task<bool> DeleteDepartmentDefaultCriterionAsync(int criterionId, int userId)
    {
        var criterion = await _taskRepository.GetDepartmentDefaultCriterionByIdAsync(criterionId);
        if (criterion == null) return false;

        if (!await CanManageDepartmentDefaultCriteriaAsync(criterion.DepartmentId, userId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Only the department manager or Admin may manage default criteria", 403);

        return await _taskRepository.DeleteDepartmentDefaultCriterionAsync(criterionId);
    }

    public async Task<IEnumerable<DepartmentDefaultCriterion>> GetDepartmentDefaultCriteriaAsync(int departmentId)
    {
        return await _taskRepository.GetDepartmentDefaultCriteriaAsync(departmentId);
    }

    // ============================================
    // Helper: Determine Rework Category (RW-1)
    // ============================================
    private string ResolveReworkCategory(string? reason, string? explicitCategory)
    {
        if (!string.IsNullOrWhiteSpace(explicitCategory))
        {
            var normalized = explicitCategory.Trim().ToLowerInvariant();
            if (ValidReworkCategories.Contains(normalized))
                return normalized;
        }

        return DetermineReworkCategory(reason);
    }

    private string DetermineReworkCategory(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return "quality";

        var reasonLower = reason.ToLowerInvariant();
        
        if (reasonLower.Contains("incomplete") || reasonLower.Contains("missing"))
            return "incomplete";
        if (reasonLower.Contains("defect") || reasonLower.Contains("bug") || reasonLower.Contains("error"))
            return "defective";
        if (reasonLower.Contains("misunderstand") || reasonLower.Contains("confusion"))
            return "misunderstood";
        if (reasonLower.Contains("change") || reasonLower.Contains("new requirement"))
            return "changed";
        
        return "quality";
    }

    // ============================================
// Bulk Operations (§5.8)
// ============================================

private static void ValidateBulkBatchSize(List<int> taskIds)
{
    if (taskIds.Count > BulkOperationLimits.MaxBatchSize)
        throw new TaskDomainException(TaskErrorCodes.LimitExceeded,
            $"Bulk operations are limited to {BulkOperationLimits.MaxBatchSize} tasks per batch", 422);
}

public async Task<BulkOperationResult> BulkUpdateStatusAsync(List<int> taskIds, string status, string? reason, int userId)
{
    ValidateBulkBatchSize(taskIds);
    var result = new BulkOperationResult();
    var correlationId = Guid.NewGuid().ToString();

    foreach (var taskId in taskIds)
    {
        try
        {
            if (!await CanEditTaskAsync(taskId, userId))
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Permission denied" });
                continue;
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Task not found" });
                continue;
            }

            if (!_allowedTransitions.ContainsKey(task.Status) || !_allowedTransitions[task.Status].Contains(status))
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = $"Cannot transition from {task.Status} to {status}" });
                continue;
            }

            // Perform the transition - this already logs activity per task
            await TransitionStatusAsync(taskId, userId, status, reason);
            result.Successful.Add(taskId);
        }
        catch (Exception ex)
        {
            result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = ex.Message });
        }
    }

    return result;
}

public async Task<BulkOperationResult> BulkUpdateAssigneeAsync(List<int> taskIds, int? assigneeId, int userId)
{
    ValidateBulkBatchSize(taskIds);
    var result = new BulkOperationResult();

    Employee? assignee = null;
    if (assigneeId.HasValue)
    {
        assignee = await _employeeRepository.GetByIdAsync(assigneeId.Value);
        if (assignee == null)
        {
            result.Failed.AddRange(taskIds.Select(id => new BulkOperationFailure { TaskId = id, Reason = "Assignee not found" }));
            return result;
        }
        if (!assignee.IsActive)
        {
            result.Failed.AddRange(taskIds.Select(id => new BulkOperationFailure { TaskId = id, Reason = "Assignee is not active" }));
            return result;
        }
    }

    foreach (var taskId in taskIds)
    {
        try
        {
            if (!await CanEditTaskAsync(taskId, userId))
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Permission denied" });
                continue;
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Task not found" });
                continue;
            }

            if (assigneeId.HasValue && assignee != null && assignee.DepartmentId != task.DepartmentId)
            {
                var userRole = await GetUserRoleAsync(userId);
                if (userRole != "Admin")
                {
                    result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Assignee must belong to the task's department" });
                    continue;
                }
            }

            // Perform assignment - this already logs activity per task
            await AssignTaskAsync(taskId, userId, assigneeId);
            result.Successful.Add(taskId);

            // CP-7: advisory, not a refusal - warn per task rather than block the assignment.
            // Checks both "on leave right now" and "on leave by this task's due date".
            if (assigneeId.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (await _leaveService.IsOnApprovedLeaveAsync(assigneeId.Value, today))
                {
                    result.Warnings[taskId] = "Assignee is on approved leave today";
                }
                else if (task.DueAt.HasValue)
                {
                    var dueDate = DateOnly.FromDateTime(task.DueAt.Value);
                    if (dueDate != today && await _leaveService.IsOnApprovedLeaveAsync(assigneeId.Value, dueDate))
                        result.Warnings[taskId] = "Assignee will be on approved leave on the task's due date";
                }
            }
        }
        catch (Exception ex)
        {
            result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = ex.Message });
        }
    }

    return result;
}
public async Task<BulkOperationResult> BulkDeleteAsync(List<int> taskIds, int userId)
{
    ValidateBulkBatchSize(taskIds);
    var result = new BulkOperationResult();

    foreach (var taskId in taskIds)
    {
        try
        {
            if (!await CanDeleteTaskAsync(taskId, userId))
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Permission denied" });
                continue;
            }

            // Delete task - this already logs activity per task
            await DeleteTaskAsync(taskId, userId);
            result.Successful.Add(taskId);
        }
        catch (Exception ex)
        {
            result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = ex.Message });
        }
    }

    return result;
}
public async Task<BulkOperationResult> BulkArchiveAsync(List<int> taskIds, bool isArchived, int userId)
{
    ValidateBulkBatchSize(taskIds);
    var result = new BulkOperationResult();

    foreach (var taskId in taskIds)
    {
        try
        {
            if (!await CanEditTaskAsync(taskId, userId))
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Permission denied" });
                continue;
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Task not found" });
                continue;
            }

            var oldValue = task.IsArchived.ToString();
            task.IsArchived = isArchived;
            task.UpdatedAt = DateTime.UtcNow;
            await _taskRepository.UpdateAsync(task);

            // Log activity per task
            await _taskRepository.AddActivityAsync(
                taskId, userId, "FIELD_UPDATED", "Archived", oldValue, isArchived.ToString(), null, Guid.NewGuid().ToString());

            result.Successful.Add(taskId);
        }
        catch (Exception ex)
        {
            result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = ex.Message });
        }
    }

    return result;
}
public async Task<BulkOperationResult> BulkAddTagsAsync(List<int> taskIds, List<string> tags, int userId)
{
    ValidateBulkBatchSize(taskIds);
    var result = new BulkOperationResult();

    if (tags == null || !tags.Any())
    {
        result.Failed.AddRange(taskIds.Select(id => new BulkOperationFailure { TaskId = id, Reason = "No tags provided" }));
        return result;
    }

    foreach (var taskId in taskIds)
    {
        try
        {
            if (!await CanEditTaskAsync(taskId, userId))
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Permission denied" });
                continue;
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Task not found" });
                continue;
            }

            foreach (var tagName in tags)
            {
                var tag = await _taskRepository.GetOrCreateTagAsync(tagName);
                await _taskRepository.AddTagToTaskAsync(taskId, tag.Id);
            }

            result.Successful.Add(taskId);
        }
        catch (Exception ex)
        {
            result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = ex.Message });
        }
    }

    return result;
}

public async Task<BulkOperationResult> BulkRemoveTagsAsync(List<int> taskIds, List<string> tags, int userId)
{
    ValidateBulkBatchSize(taskIds);
    var result = new BulkOperationResult();

    if (tags == null || !tags.Any())
    {
        result.Failed.AddRange(taskIds.Select(id => new BulkOperationFailure { TaskId = id, Reason = "No tags provided" }));
        return result;
    }

    foreach (var taskId in taskIds)
    {
        try
        {
            if (!await CanEditTaskAsync(taskId, userId))
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Permission denied" });
                continue;
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = "Task not found" });
                continue;
            }

            foreach (var tagName in tags)
            {
                var tag = await _taskRepository.FindTagByNameAsync(tagName);
                if (tag != null)
                {
                    await _taskRepository.RemoveTagFromTaskAsync(taskId, tag.Id);
                }
            }

            result.Successful.Add(taskId);
        }
        catch (Exception ex)
        {
            result.Failed.Add(new BulkOperationFailure { TaskId = taskId, Reason = ex.Message });
        }
    }

    return result;
}
}
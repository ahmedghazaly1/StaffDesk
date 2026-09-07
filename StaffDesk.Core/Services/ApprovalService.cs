using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class ApprovalService : IApprovalService
{
    private readonly IApprovalRepository _approvalRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDelegationService _delegationService;
    private readonly IAuditService _auditService;
    private readonly IUserRepository _userRepository;

    public ApprovalService(
        IApprovalRepository approvalRepository,
        ITaskRepository taskRepository,
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        IDelegationService delegationService,
        IAuditService auditService,
        IUserRepository userRepository)
    {
        _approvalRepository = approvalRepository;
        _taskRepository = taskRepository;
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _delegationService = delegationService;
        _auditService = auditService;
        _userRepository = userRepository;
    }

    // ============================================
    // Create approval for a task
    // ============================================
    public async Task<TaskApproval> CreateApprovalAsync(
        int taskId,
        string name,
        string? description,
        bool isRequired,
        List<(int? ApproverId, string? Role, int Order)> steps,
        int createdBy)
    {
        // Check if task exists
        var foundTask = await _taskRepository.GetByIdAsync(taskId);
        if (foundTask == null)
            throw new ArgumentException("Task not found");

        // Check if approval already exists for this task
        var existingApproval = await _approvalRepository.GetApprovalByTaskIdAsync(taskId);
        if (existingApproval != null)
            throw new InvalidOperationException("An approval already exists for this task");

        // Validate steps
        if (steps == null || !steps.Any())
            throw new ArgumentException("At least one approval step is required");

        // Validate each step
        foreach (var step in steps)
        {
            if (!step.ApproverId.HasValue && string.IsNullOrEmpty(step.Role))
                throw new ArgumentException("Each step must have either an ApproverId or a Role");

            if (step.ApproverId.HasValue)
            {
                var approver = await _employeeRepository.GetByIdAsync(step.ApproverId.Value);
                if (approver == null)
                    throw new ArgumentException($"Approver with ID {step.ApproverId} not found");
                if (!approver.IsActive)
                    throw new ArgumentException($"Approver {approver.FullName} is not active");
            }

            if (step.Order < 0)
                throw new ArgumentException("Order must be a positive number");
        }

        // Create approval
        var newApproval = new TaskApproval
        {
            TaskId = taskId,
            Name = name,
            Description = description,
            IsRequired = isRequired,
            CreatedAt = DateTime.UtcNow
        };

        var createdApproval = await _approvalRepository.CreateApprovalAsync(newApproval);

        // Add steps WITH TaskApprovalId
        foreach (var step in steps.OrderBy(s => s.Order))
        {
            var approvalStep = new ApprovalStep
            {
                TaskId = taskId,
                TaskApprovalId = createdApproval.Id,  // FIX: Link to approval
                ApproverId = step.ApproverId,
                Role = step.Role,
                Order = step.Order,
                State = "PENDING",
                CreatedAt = DateTime.UtcNow
            };
            await _approvalRepository.AddStepAsync(approvalStep);
        }

        // Mark task as requiring approval
        foundTask.IsApprovalRequired = isRequired;
        await _taskRepository.UpdateAsync(foundTask);

        // Add activity
        await _taskRepository.AddActivityAsync(
            taskId,
            createdBy,
            "APPROVAL_CREATED",
            null,
            null,
            $"{steps.Count} steps",
            null,
            Guid.NewGuid().ToString()
        );

        // Notify first approver
        var firstStep = await _approvalRepository.GetNextPendingStepAsync(taskId);
        if (firstStep?.ApproverId.HasValue == true)
        {
            await _taskRepository.CreateNotificationAsync(
                firstStep.ApproverId.Value,
                "APPROVAL_REQUESTED",
                $"Your approval is required for task {foundTask.Key}: {foundTask.Title}",
                taskId
            );
        }

        return await _approvalRepository.GetApprovalByIdAsync(createdApproval.Id) ?? createdApproval;
    }

    // ============================================
    // Get approval by task ID
    // ============================================
    public async Task<TaskApproval?> GetApprovalByTaskIdAsync(int taskId)
    {
        return await _approvalRepository.GetApprovalByTaskIdAsync(taskId);
    }

    // ============================================
    // Get approval by ID
    // ============================================
    public async Task<TaskApproval?> GetApprovalByIdAsync(int id)
    {
        return await _approvalRepository.GetApprovalByIdAsync(id);
    }

    // ============================================
    // Approve or reject a step
    // ============================================
    public async Task<ApprovalStep> ReviewStepAsync(int stepId, int reviewerId, string state, string? decisionNote)
    {
        if (state != "APPROVED" && state != "REJECTED")
            throw new ArgumentException("State must be APPROVED or REJECTED");

        var targetStep = await _approvalRepository.GetStepByIdAsync(stepId);
        if (targetStep == null)
            throw new ArgumentException("Approval step not found");

        if (targetStep.State != "PENDING")
            throw new InvalidOperationException($"This step is already {targetStep.State}");

        // AP-1: steps are evaluated in order - the lowest-order PENDING step for this task must
        // be reviewed before a later one, regardless of who happens to call this endpoint first.
        var nextPending = await _approvalRepository.GetNextPendingStepAsync(targetStep.TaskId);
        if (nextPending != null && nextPending.Id != targetStep.Id)
            throw new InvalidOperationException(
                $"Step {targetStep.Order} cannot be reviewed before step {nextPending.Order}, which is still pending");

        int? onBehalfOfId = null;
        await AuthorizeReviewerAsync(targetStep, reviewerId, onBehalf => onBehalfOfId = onBehalf);

        var effectiveApproverId = onBehalfOfId ?? reviewerId;

        // AP-3: neither the acting reviewer nor the person they cover may approve their own assignment.
        if (state == "APPROVED")
        {
            var approvalTask = await _taskRepository.GetByIdAsync(targetStep.TaskId);
            if (approvalTask?.AssigneeId == reviewerId || approvalTask?.AssigneeId == effectiveApproverId)
                throw new InvalidOperationException("You cannot approve a task you are assigned to");
        }

        // AP-4: Decision is immutable once recorded
        targetStep.State = state;
        targetStep.DecisionNote = decisionNote;
        targetStep.DecisionAt = DateTime.UtcNow;

        var updatedStep = await _approvalRepository.UpdateStepAsync(targetStep);

        // Add activity
        await _taskRepository.AddActivityAsync(
            targetStep.TaskId,
            reviewerId,
            state == "APPROVED" ? "APPROVAL_APPROVED" : "APPROVAL_REJECTED",
            "ApprovalStep",
            null,
            $"Step {targetStep.Order}: {state}",
            decisionNote,
            Guid.NewGuid().ToString()
        );

        await LogApprovalAuditAsync(
            reviewerId,
            state == "APPROVED" ? "APPROVAL_STEP_APPROVED" : "APPROVAL_STEP_REJECTED",
            targetStep.TaskId,
            targetStep.Id,
            onBehalfOfId,
            state,
            decisionNote);

        // If rejected, update task approval status
        if (state == "REJECTED")
        {
            await UpdateTaskApprovalStatusAsync(targetStep.TaskId);
            
            var rejectedTask = await _taskRepository.GetByIdAsync(targetStep.TaskId);
            if (rejectedTask != null)
            {
                await _taskRepository.CreateNotificationAsync(
                    rejectedTask.CreatedById,
                    "APPROVAL_REJECTED",
                    $"Task {rejectedTask.Key}: {rejectedTask.Title} was rejected",
                    targetStep.TaskId
                );
            }
        }
        else
        {
            // Check if all steps are approved
            var allApprovalSteps = await _approvalRepository.GetStepsByTaskIdAsync(targetStep.TaskId);
            if (allApprovalSteps.All(s => s.State == "APPROVED"))
            {
                await UpdateTaskApprovalStatusAsync(targetStep.TaskId);
                
                var completedTask = await _taskRepository.GetByIdAsync(targetStep.TaskId);
                if (completedTask != null)
                {
                    await _taskRepository.CreateNotificationAsync(
                        completedTask.CreatedById,
                        "APPROVAL_COMPLETED",
                        $"Task {completedTask.Key}: {completedTask.Title} is fully approved",
                        targetStep.TaskId
                    );
                }
            }
            else
            {
                // Notify next approver
                var nextPendingStep = await _approvalRepository.GetNextPendingStepAsync(targetStep.TaskId);
                if (nextPendingStep?.ApproverId.HasValue == true)
                {
                    var nextApprovalTask = await _taskRepository.GetByIdAsync(targetStep.TaskId);
                    await _taskRepository.CreateNotificationAsync(
                        nextPendingStep.ApproverId.Value,
                        "APPROVAL_REQUESTED",
                        $"Your approval is required for task {nextApprovalTask?.Key}: {nextApprovalTask?.Title}",
                        targetStep.TaskId
                    );
                }
            }
        }

        return updatedStep;
    }

    // ============================================
    // Check if a task is fully approved
    // ============================================
    public async Task<bool> IsFullyApprovedAsync(int taskId)
    {
        var approvalSteps = await _approvalRepository.GetStepsByTaskIdAsync(taskId);
        if (!approvalSteps.Any()) return true;
        return approvalSteps.All(s => s.State == "APPROVED" || s.State == "SKIPPED");
    }

    // ============================================
    // Get pending approvals for a user
    // ============================================
    public async Task<IEnumerable<ApprovalStep>> GetPendingApprovalsForUserAsync(int userId)
    {
        return await _approvalRepository.GetPendingStepsForApproverAsync(userId);
    }

    // ============================================
    // Update task approval status
    // ============================================
    public async Task UpdateTaskApprovalStatusAsync(int taskId)
    {
        var taskToUpdate = await _taskRepository.GetByIdAsync(taskId);
        if (taskToUpdate == null) return;

        var taskSteps = await _approvalRepository.GetStepsByTaskIdAsync(taskId);
        if (!taskSteps.Any()) return;

        if (taskSteps.Any(s => s.State == "REJECTED"))
        {
            // Task has been rejected - return to IN_PROGRESS
            taskToUpdate.Status = "IN_PROGRESS";
            await _taskRepository.UpdateAsync(taskToUpdate);
        }
        else if (taskSteps.All(s => s.State == "APPROVED" || s.State == "SKIPPED"))
        {
            // All steps approved
            taskToUpdate.IsApprovalRequired = false;
            await _taskRepository.UpdateAsync(taskToUpdate);
        }
    }

    // ============================================
    // Skip a step (Admin/Manager only)
    // ============================================
    public async Task<ApprovalStep> SkipStepAsync(int stepId, int userId, string? reason)
    {
        var stepToSkip = await _approvalRepository.GetStepByIdAsync(stepId);
        if (stepToSkip == null)
            throw new ArgumentException("Approval step not found");

        if (stepToSkip.State != "PENDING")
            throw new InvalidOperationException($"This step is already {stepToSkip.State}");

        // WC-29: skip is a held Admin/manager permission, not something delegation grants or removes.
        var userRole = await GetUserRoleAsync(userId);
        var skipTask = await _taskRepository.GetByIdAsync(stepToSkip.TaskId);
        
        var isAuthorized = userRole == "Admin" || 
                          (skipTask != null && await IsDepartmentManagerAsync(userId, skipTask.DepartmentId));

        if (!isAuthorized)
            throw new UnauthorizedAccessException("Only Admin or Department Manager can skip approval steps");

        stepToSkip.State = "SKIPPED";
        stepToSkip.DecisionNote = reason ?? "Skipped by authorized user";
        stepToSkip.DecisionAt = DateTime.UtcNow;

        var updatedSkippedStep = await _approvalRepository.UpdateStepAsync(stepToSkip);

        // Add activity
        await _taskRepository.AddActivityAsync(
            stepToSkip.TaskId,
            userId,
            "APPROVAL_SKIPPED",
            "ApprovalStep",
            null,
            $"Step {stepToSkip.Order}: SKIPPED",
            reason,
            Guid.NewGuid().ToString()
        );

        // Check if all steps are approved/skipped
        var remainingSteps = await _approvalRepository.GetStepsByTaskIdAsync(stepToSkip.TaskId);
        if (remainingSteps.All(s => s.State == "APPROVED" || s.State == "SKIPPED"))
        {
            await UpdateTaskApprovalStatusAsync(stepToSkip.TaskId);
            
            var skippedTask = await _taskRepository.GetByIdAsync(stepToSkip.TaskId);
            if (skippedTask != null)
            {
                await _taskRepository.CreateNotificationAsync(
                    skippedTask.CreatedById,
                    "APPROVAL_COMPLETED",
                    $"Task {skippedTask.Key}: {skippedTask.Title} is fully approved",
                    stepToSkip.TaskId
                );
            }
        }

        return updatedSkippedStep;
    }

    public async Task<ApprovalStep> ReassignStepAsync(int stepId, int newApproverId, int userId, string? reason)
    {
        var step = await _approvalRepository.GetStepByIdAsync(stepId);
        if (step == null)
            throw new ArgumentException("Approval step not found");

        if (step.State != "PENDING")
            throw new InvalidOperationException($"This step is already {step.State}");

        var userRole = await GetUserRoleAsync(userId);
        var task = await _taskRepository.GetByIdAsync(step.TaskId);

        var isAuthorized = userRole == "Admin" ||
                          (task != null && await IsDepartmentManagerAsync(userId, task.DepartmentId));

        if (!isAuthorized)
            throw new UnauthorizedAccessException("Only Admin or Department Manager can reassign approval steps");

        var newApprover = await _employeeRepository.GetByIdAsync(newApproverId);
        if (newApprover == null)
            throw new ArgumentException("New approver not found");
        if (!newApprover.IsActive)
            throw new ArgumentException("New approver is not active");

        var oldApproverId = step.ApproverId;
        step.ApproverId = newApproverId;
        step.Role = null;
        var updatedStep = await _approvalRepository.UpdateStepAsync(step);

        await _taskRepository.AddActivityAsync(
            step.TaskId,
            userId,
            "APPROVAL_REASSIGNED",
            "ApprovalStep",
            oldApproverId?.ToString(),
            newApproverId.ToString(),
            reason,
            Guid.NewGuid().ToString());

        await LogApprovalAuditAsync(userId, "APPROVAL_STEP_REASSIGNED", step.TaskId, step.Id, null, "REASSIGNED", reason);

        if (task != null)
        {
            await _taskRepository.CreateNotificationAsync(
                newApproverId,
                "APPROVAL_REQUESTED",
                $"Your approval is required for task {task.Key}: {task.Title}",
                step.TaskId);
        }

        return updatedStep;
    }

    // ============================================
    // Delete approval
    // ============================================
    public async Task<bool> DeleteApprovalAsync(int approvalId, int userId)
    {
        var approvalToDelete = await _approvalRepository.GetApprovalByIdAsync(approvalId);
        if (approvalToDelete == null) return false;

        // Check authorization
        var userRole = await GetUserRoleAsync(userId);
        var deleteTask = await _taskRepository.GetByIdAsync(approvalToDelete.TaskId);
        
        var isAuthorized = userRole == "Admin" || 
                          (deleteTask != null && await IsDepartmentManagerAsync(userId, deleteTask.DepartmentId));

        if (!isAuthorized)
            throw new UnauthorizedAccessException("Only Admin or Department Manager can delete approvals");

        // Reset task approval flag
        if (deleteTask != null)
        {
            deleteTask.IsApprovalRequired = false;
            await _taskRepository.UpdateAsync(deleteTask);
        }

        return await _approvalRepository.DeleteApprovalAsync(approvalId);
    }

    // ============================================
    // Helper Methods
    // ============================================

    private async Task<string> GetUserRoleAsync(int userId)
    {
        var user = await _userRepository.GetByEmployeeIdAsync(userId);
        return user?.Role ?? "Member";
    }

    private async Task LogApprovalAuditAsync(
        int actorId,
        string eventType,
        int taskId,
        int stepId,
        int? onBehalfOfId,
        string outcome,
        string? note)
    {
        var actor = await _employeeRepository.GetByIdAsync(actorId);
        await _auditService.LogAsync(
            eventType,
            actorId,
            actor != null ? $"{actor.FullName} ({actor.JobTitle})" : actorId.ToString(),
            outcome,
            "ApprovalStep",
            stepId.ToString(),
            onBehalfOfId,
            new { taskId, stepId, note },
            null,
            null,
            null);
    }

    private async Task AuthorizeReviewerAsync(ApprovalStep targetStep, int reviewerId, Action<int?> setOnBehalfOf)
    {
        if (targetStep.ApproverId.HasValue)
        {
            if (targetStep.ApproverId.Value == reviewerId)
                return;

            var isDelegate = await _delegationService.IsDelegateForDelegatorAsync(
                reviewerId, targetStep.ApproverId.Value, "APPROVALS");
            if (isDelegate)
            {
                setOnBehalfOf(targetStep.ApproverId.Value);
                return;
            }

            var reviewerTask = await _taskRepository.GetByIdAsync(targetStep.TaskId);
            var userRole = await GetUserRoleAsync(reviewerId);
            var isAuthorized = userRole == "Admin" ||
                               (reviewerTask != null && await IsDepartmentManagerAsync(reviewerId, reviewerTask.DepartmentId));
            if (!isAuthorized)
                throw new UnauthorizedAccessException("You are not authorized to review this step");
            return;
        }

        if (!string.IsNullOrWhiteSpace(targetStep.Role))
        {
            var userRole = await GetUserRoleAsync(reviewerId);
            if (!RolesMatch(userRole, targetStep.Role))
                throw new UnauthorizedAccessException($"This step requires role {targetStep.Role}");
            return;
        }

        throw new UnauthorizedAccessException("You are not authorized to review this step");
    }

    private static bool RolesMatch(string userRole, string requiredRole)
    {
        if (string.Equals(userRole, requiredRole, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(requiredRole, "MANAGER", StringComparison.OrdinalIgnoreCase)
            && string.Equals(userRole, User.Roles.Manager, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(requiredRole, "HR_ADMIN", StringComparison.OrdinalIgnoreCase)
            && string.Equals(userRole, User.Roles.HrAdmin, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private async Task<bool> IsDepartmentManagerAsync(int userId, int departmentId)
    {
        var dept = await _departmentRepository.GetByIdAsync(departmentId);
        return dept?.ManagerId == userId;
    }
}
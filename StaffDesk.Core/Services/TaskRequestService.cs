using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

// Part B Module 2 - Intake and Triage (WC-1..WC-9).
public class TaskRequestService : ITaskRequestService
{
    private static readonly string[] ValidDeclineReasonCategories =
        { "duplicate", "out_of_scope", "insufficient_information", "not_now", "will_not_do" };

    // WC-3: SUBMITTED and UNDER_TRIAGE are treated as the same visible queue and the request
    // auto-transitions on the accept/decline/merge call itself, rather than modelling "claiming"
    // as its own separate transition - simpler, and there's no separate "claim" endpoint in the
    // spec's Part B endpoint list to hang that on.
    private static readonly string[] DecidableStatuses = { "SUBMITTED", "UNDER_TRIAGE" };

    private readonly ITaskRequestRepository _requestRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserRepository _userRepository;

    public TaskRequestService(
        ITaskRequestRepository requestRepository,
        ITaskRepository taskRepository,
        IDepartmentRepository departmentRepository,
        IUserRepository userRepository)
    {
        _requestRepository = requestRepository;
        _taskRepository = taskRepository;
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
    }

    public async Task<TaskRequest> SubmitAsync(
        int requestedById,
        string title,
        string? description,
        int departmentId,
        string? businessJustification,
        DateTime? desiredByDate)
    {
        title = title?.Trim() ?? string.Empty;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(title) || title.Length < 3 || title.Length > 140)
            errors.Add("Title must be between 3 and 140 characters");

        // WC-2: deliberately NO "must belong to this department" check - any authenticated
        // employee may raise a request against any department.
        var department = await _departmentRepository.GetByIdAsync(departmentId);
        if (department == null)
            errors.Add("Department does not exist");

        if (errors.Any())
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Validation failed", 400, errors);

        var now = DateTime.UtcNow;
        var request = new TaskRequest
        {
            Title = title,
            Description = description?.Trim(),
            RequestedById = requestedById,
            DepartmentId = departmentId,
            BusinessJustification = businessJustification?.Trim(),
            DesiredByDate = desiredByDate?.ToUniversalTime(),
            Status = "SUBMITTED",
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await _requestRepository.CreateAsync(request);
        await NotifyTriageAuthoritiesOfSubmissionAsync(created, department!);
        return created;
    }

    public async Task<TaskRequest?> GetRequestAsync(int id, int viewerId)
    {
        var request = await _requestRepository.GetByIdAsync(id);
        if (request == null) return null;

        if (request.RequestedById == viewerId) return request;
        if (await IsTriageAuthorityAsync(viewerId, request.DepartmentId)) return request;

        return null;
    }

    public async Task<(IEnumerable<TaskRequest> Items, int TotalCount)> GetListAsync(
        int viewerId, int? departmentId = null, string? status = null, int? page = null, int? limit = null)
    {
        return await _requestRepository.GetFilteredAsync(viewerId, departmentId, status, page, limit);
    }

    public async Task<List<TaskRequest>> GetTriageQueueAsync(int? departmentId, int actorId)
    {
        // WC-8: departmentId is documented as optional - a caller omitting it should see their
        // own department(s), not silently bind to departmentId=0 and fail authority for a
        // department that doesn't exist (the previous behavior, which surfaced as an opaque 403).
        if (departmentId.HasValue)
        {
            if (!await IsTriageAuthorityAsync(actorId, departmentId.Value))
                throw new TaskDomainException(TaskErrorCodes.Forbidden,
                    "Only the department manager or Admin may view the triage queue", 403);

            return await _requestRepository.GetTriageQueueAsync(departmentId.Value);
        }

        var user = await _userRepository.GetByEmployeeIdAsync(actorId);
        if (user?.Role == "Admin")
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "departmentId is required for Admin - Admin has no single department to default to", 400);

        var managedDeptIds = await _departmentRepository.GetManagedDepartmentIdsAsync(actorId);
        if (managedDeptIds.Count == 0)
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "departmentId is required - you don't manage or triage for any department", 400);

        var merged = new List<TaskRequest>();
        foreach (var deptId in managedDeptIds)
            merged.AddRange(await _requestRepository.GetTriageQueueAsync(deptId));

        return merged.OrderBy(r => r.SubmittedAt).ToList();
    }

    public async Task<TaskRequest> AcceptAsync(int requestId, int actorId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Request not found", 404);

        if (!await IsTriageAuthorityAsync(actorId, request.DepartmentId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden,
                "Only the department manager or Admin may triage this request", 403);

        if (!DecidableStatuses.Contains(request.Status))
            throw new TaskDomainException(TaskErrorCodes.TaskRequestInvalidTransition,
                $"Cannot accept a request in status {request.Status}", 409);

        WorkTask createdTask = null!;

        // WC-6: task creation happens in ONE transaction. This deliberately does NOT call through
        // ITaskService.CreateTaskAsync, which owns and commits its own transaction internally -
        // nesting a second BeginTransactionAsync on the same DbContext inside this one would fail.
        // The task fields were already validated at submission time (title/department), so building
        // the WorkTask directly here (mirroring CreateTaskAsync's happy-path body) is safe.
        await _taskRepository.ExecuteInTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;

            var task = new WorkTask
            {
                Title = request.Title,
                Description = request.Description, // WC-6: carried across verbatim.
                DepartmentId = request.DepartmentId,
                CreatedById = actorId, // WC-6: the ACCEPTING employee (triager) is the task's creator.
                AssigneeId = request.RequestedById, // The requester owns the work after accept.
                Priority = "NORMAL",
                Status = "OPEN",
                SourceRequestId = request.Id,
                IsArchived = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            createdTask = await _taskRepository.CreateAsync(task);

            await _taskRepository.AddActivityAsync(
                createdTask.Id, actorId, "CREATED", null, null, null, null, Guid.NewGuid().ToString());

            await _taskRepository.AddActivityAsync(
                createdTask.Id, actorId, "ASSIGNED", "Assignee",
                null, request.RequestedById.ToString(), null, Guid.NewGuid().ToString());

            // Module 1 (WC-23): open the first status interval, same as any other task creation path.
            await _taskRepository.OpenStatusIntervalAsync(createdTask.Id, createdTask.Status, actorId, createdTask.CreatedAt);

            // Module 3 (WC-12): copy the department's default Definition of Done onto the new task.
            await _taskRepository.CopyDepartmentDefaultCriteriaAsync(createdTask.Id, request.DepartmentId);

            await _taskRepository.AddWatcherAsync(createdTask.Id, actorId);
            if (request.RequestedById != actorId)
                await _taskRepository.AddWatcherAsync(createdTask.Id, request.RequestedById);

            request.Status = "ACCEPTED";
            request.CreatedTaskId = createdTask.Id;
            request.TriageDecidedAt = now;
            request.UpdatedAt = now;
            await _requestRepository.UpdateAsync(request);

            await _taskRepository.CreateNotificationAsync(
                request.RequestedById,
                "TASK_REQUEST_ACCEPTED",
                $"Your request '{request.Title}' was accepted and created as task {createdTask.Key}",
                createdTask.Id
            );
        });

        return await _requestRepository.GetByIdAsync(requestId) ?? request;
    }

    public async Task<TaskRequest> DeclineAsync(int requestId, int actorId, string reasonCategory, string note)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Request not found", 404);

        if (!await IsTriageAuthorityAsync(actorId, request.DepartmentId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden,
                "Only the department manager or Admin may triage this request", 403);

        if (!DecidableStatuses.Contains(request.Status))
            throw new TaskDomainException(TaskErrorCodes.TaskRequestInvalidTransition,
                $"Cannot decline a request in status {request.Status}", 409);

        // WC-4: both a taxonomy reason AND a free-text note are required.
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(reasonCategory) || !ValidDeclineReasonCategories.Contains(reasonCategory))
            errors.Add("reasonCategory must be one of: " + string.Join(", ", ValidDeclineReasonCategories));
        if (string.IsNullOrWhiteSpace(note))
            errors.Add("A free-text note is required when declining a request");
        if (errors.Any())
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Validation failed", 400, errors);

        var now = DateTime.UtcNow;
        request.Status = "DECLINED";
        request.DeclineReasonCategory = reasonCategory;
        request.DeclineNote = note.Trim();
        request.TriageDecidedAt = now;
        request.UpdatedAt = now;
        await _requestRepository.UpdateAsync(request);

        await _taskRepository.CreateNotificationAsync(
            request.RequestedById,
            "TASK_REQUEST_DECLINED",
            $"Your request '{request.Title}' was declined ({reasonCategory}): {request.DeclineNote}",
            null
        );

        return request;
    }

    public async Task<TaskRequest> MergeAsync(int requestId, int actorId, int mergedIntoRequestId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Request not found", 404);

        if (!await IsTriageAuthorityAsync(actorId, request.DepartmentId))
            throw new TaskDomainException(TaskErrorCodes.Forbidden,
                "Only the department manager or Admin may triage this request", 403);

        if (!DecidableStatuses.Contains(request.Status))
            throw new TaskDomainException(TaskErrorCodes.TaskRequestInvalidTransition,
                $"Cannot merge a request in status {request.Status}", 409);

        if (mergedIntoRequestId == requestId)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "A request cannot be merged into itself", 400);

        var target = await _requestRepository.GetByIdAsync(mergedIntoRequestId);
        if (target == null)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Target request does not exist", 400);

        var now = DateTime.UtcNow;
        request.Status = "MERGED";
        request.MergedIntoRequestId = mergedIntoRequestId;
        request.TriageDecidedAt = now;
        request.UpdatedAt = now;
        await _requestRepository.UpdateAsync(request);

        await _taskRepository.CreateNotificationAsync(
            request.RequestedById,
            "TASK_REQUEST_MERGED",
            $"Your request '{request.Title}' was merged into another request",
            null
        );

        return request;
    }

    private async Task NotifyTriageAuthoritiesOfSubmissionAsync(TaskRequest request, Department department)
    {
        var recipients = new HashSet<int>();
        if (department.ManagerId is int managerId)
            recipients.Add(managerId);

        var triagers = await _departmentRepository.GetTriagersAsync(request.DepartmentId);
        foreach (var triager in triagers)
            recipients.Add(triager.EmployeeId);

        foreach (var adminEmployeeId in await _userRepository.GetEmployeeIdsByRoleAsync(User.Roles.Admin))
            recipients.Add(adminEmployeeId);

        recipients.Remove(request.RequestedById);

        var departmentName = string.IsNullOrWhiteSpace(department.Name) ? "a department" : department.Name;
        foreach (var recipientId in recipients)
        {
            await _taskRepository.CreateNotificationAsync(
                recipientId,
                "TASK_REQUEST_SUBMITTED",
                $"New task request '{request.Title}' was submitted for {departmentName} and needs triage",
                null
            );
        }
    }

    // WC-7: triage authority = department manager, designated triager, or Admin.
    public async Task<bool> IsTriageAuthorityAsync(int actorId, int departmentId)
    {
        var user = await _userRepository.GetByEmployeeIdAsync(actorId);
        if (user?.Role == "Admin") return true;

        var dept = await _departmentRepository.GetByIdAsync(departmentId);
        if (dept?.ManagerId == actorId) return true;

        return await _departmentRepository.IsTriagerAsync(departmentId, actorId);
    }

    public async Task<TriageLatencyMetrics> GetTriageLatencyMetricsAsync(int? departmentId, DateTime? fromDate, DateTime? toDate)
    {
        var decided = await _requestRepository.GetDecidedForMetricsAsync(departmentId, fromDate, toDate);
        var latencies = decided
            .Where(r => r.SubmittedAt != default && r.TriageDecidedAt.HasValue)
            .Select(r => (r.TriageDecidedAt!.Value - r.SubmittedAt).TotalMinutes)
            .OrderBy(m => m)
            .ToList();

        if (!latencies.Any())
        {
            return new TriageLatencyMetrics();
        }

        var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        p95Index = Math.Max(0, Math.Min(p95Index, latencies.Count - 1));

        var medianIndex = latencies.Count / 2;

        return new TriageLatencyMetrics
        {
            TotalDecided = latencies.Count,
            AverageMinutes = latencies.Average(),
            MedianMinutes = latencies[medianIndex],
            P95Minutes = latencies[p95Index]
        };
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StaffDesk.API.Common;
using StaffDesk.API.DTOs;
using StaffDesk.API.Filters;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Entities;

namespace StaffDesk.API.Controllers;

[ApiController]
[Route("v1/tasks")]
[Authorize]
public class TasksController : ApiControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ISlaService _slaService;
    private readonly ILeaveService _leaveService;

    // Allow-list for GET /v1/tasks query params - anything else is rejected (VE-4 UNKNOWN_QUERY_PARAM).
    private static readonly HashSet<string> AllowedTaskQueryParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "priority", "assigneeId", "createdById", "departmentId", "watchedByMe", "tag",
        "search", "dueBefore", "dueAfter", "overdue", "includeArchived", "parentTaskId", "sort",
        "page", "limit"
    };

    public TasksController(
        ITaskService taskService,
        ISlaService slaService,
        ILeaveService leaveService,
        IUserRepository userRepository)
        : base(userRepository)
    {
        _taskService = taskService;
        _slaService = slaService;
        _leaveService = leaveService;
    }

    // ============================================
    // GET /v1/tasks - List tasks with filters
    // ============================================
    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromQuery] int? page = 1,
        [FromQuery] int? limit = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? assigneeId = null,
        [FromQuery] int? departmentId = null,
        [FromQuery] string? createdById = null,
        [FromQuery] bool? watchedByMe = null,
        [FromQuery] string? tag = null,
        [FromQuery] DateTime? dueBefore = null,
        [FromQuery] DateTime? dueAfter = null,
        [FromQuery] bool? overdue = null,
        [FromQuery] bool? includeArchived = false,
        [FromQuery] int? parentTaskId = null,
        [FromQuery] string? sort = "-createdAt")
    {
        var unknownParams = Request.Query.Keys.Where(k => !AllowedTaskQueryParams.Contains(k)).ToList();
        if (unknownParams.Any())
        {
            return BadRequest(ApiError.Build(TaskErrorCodes.UnknownQueryParam,
                $"Unrecognized query parameter(s): {string.Join(", ", unknownParams)}"));
        }

        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            // SRS 11.1: assigneeId/createdById accept "me"/"unassigned" special values in addition to an id.
            int? resolvedAssigneeId = null;
            bool filterUnassigned = false;
            if (!string.IsNullOrWhiteSpace(assigneeId))
            {
                if (assigneeId.Equals("me", StringComparison.OrdinalIgnoreCase))
                    resolvedAssigneeId = employeeId.Value;
                else if (assigneeId.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
                    filterUnassigned = true;
                else if (int.TryParse(assigneeId, out var parsedAssignee))
                    resolvedAssigneeId = parsedAssignee;
                else
                    return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "assigneeId must be an integer, 'me', or 'unassigned'"));
            }

            int? resolvedCreatedById = null;
            if (!string.IsNullOrWhiteSpace(createdById))
            {
                if (createdById.Equals("me", StringComparison.OrdinalIgnoreCase))
                    resolvedCreatedById = employeeId.Value;
                else if (int.TryParse(createdById, out var parsedCreatedBy))
                    resolvedCreatedById = parsedCreatedBy;
                else
                    return BadRequest(ApiError.Build(TaskErrorCodes.ValidationError, "createdById must be an integer or 'me'"));
            }

            // LS-1: "unassigned" is pushed into the same DB query as every other filter, so
            // pagination and Total stay correct (never filtered after Skip/Take).
            var (items, total) = await _taskService.GetTasksAsync(
                employeeId.Value, page, limit, search, status, priority,
                resolvedAssigneeId, departmentId, resolvedCreatedById, watchedByMe,
                tag, dueBefore, dueAfter, overdue, includeArchived,
                parentTaskId, sort, filterUnassigned ? true : null
            );

            var data = new List<TaskResponseDto>();
            foreach (var t in items)
                data.Add(await MapToResponseAsync(t, employeeId.Value));

            var response = new TaskListResponseDto
            {
                Data = data,
                Page = page ?? 1,
                Limit = limit ?? 25,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / (limit ?? 25))
            };

            return Ok(response);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/tasks/mine - My active tasks
    // ============================================
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTasks()
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var tasks = await _taskService.GetMyTasksAsync(employeeId.Value);
            var data = new List<TaskResponseDto>();
            foreach (var t in tasks)
                data.Add(await MapToResponseAsync(t, employeeId.Value));
            return Ok(data);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/tasks/{id} - Get task by ID
    // ============================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTask(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByIdAsync(id, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        ConcurrencyHelper.SetETag(Response, task.UpdatedAt);
        return Ok(await MapToResponseAsync(task, employeeId.Value));
    }

    // ============================================
    // GET /v1/tasks/key/{key} - Get task by key
    // ============================================
    [HttpGet("key/{key}")]
    public async Task<IActionResult> GetTaskByKey(string key)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var task = await _taskService.GetTaskByKeyAsync(key, employeeId.Value);
        if (task == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

        return Ok(await MapToResponseAsync(task, employeeId.Value));
    }

    // ============================================
    // POST /v1/tasks - Create a task
    // ============================================
    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] TaskCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var task = await _taskService.CreateTaskAsync(
                dto.Title,
                dto.Description,
                dto.DepartmentName,
                employeeId.Value,
                dto.AssigneeId,
                dto.Priority,
                dto.DueAt,
                dto.EstimateMinutes,
                dto.ParentTaskId,
                dto.Tags
            );

            var response = await MapToResponseAsync(task, employeeId.Value);
            await AttachLeaveWarningAsync(response, dto.AssigneeId);
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, response);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // PUT /v1/tasks/{id} - Update a task
    // ============================================
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var current = await _taskService.GetTaskByIdAsync(id, employeeId.Value);
        if (current == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));
        if (ConcurrencyHelper.RequireIfMatch(Request, current.UpdatedAt) is { } precondition)
            return StatusCode(precondition.Status, precondition.Body);

        try
        {
            // True partial update: a field absent from the request body keeps its current value
            // instead of being overwritten with the DTO's C# default (null/""/false), which
            // previously wiped assignee/description/etc. whenever a caller sent a partial object.
            // Nullable fields need this to tell "omitted" apart from "explicitly cleared" -
            // AssigneeId/Description/DueAt/EstimateMinutes/ParentTaskId can each be nulled out on
            // purpose (unassign, clear the due date, ...), which C# defaults alone can't express.
            var provided = await GetProvidedJsonFieldsAsync();

            var task = await _taskService.UpdateTaskAsync(
                id,
                employeeId.Value,
                provided.Contains("title") ? dto.Title : current.Title,
                provided.Contains("description") ? dto.Description : current.Description,
                provided.Contains("departmentName") ? dto.DepartmentName : (current.Department?.Name ?? string.Empty),
                provided.Contains("assigneeId") ? dto.AssigneeId : current.AssigneeId,
                provided.Contains("priority") ? dto.Priority : current.Priority,
                provided.Contains("dueAt") ? dto.DueAt : current.DueAt,
                provided.Contains("estimateMinutes") ? dto.EstimateMinutes : current.EstimateMinutes,
                provided.Contains("parentTaskId") ? dto.ParentTaskId : current.ParentTaskId,
                provided.Contains("isArchived") ? dto.IsArchived : current.IsArchived
            );

            var response = await MapToResponseAsync(task, employeeId.Value);
            await AttachLeaveWarningAsync(response, provided.Contains("assigneeId") ? dto.AssigneeId : current.AssigneeId);
            ConcurrencyHelper.SetETag(Response, task.UpdatedAt);
            return Ok(response);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // Reads the raw request body to determine which top-level JSON properties were actually
    // present, so PUT /v1/tasks/{id} can do a genuine partial update. Works whether or not an
    // earlier middleware already buffered the body (EnableBuffering is idempotent).
    private async Task<HashSet<string>> GetProvidedJsonFieldsAsync()
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(Request.Body, default, HttpContext.RequestAborted);
            Request.Body.Position = 0;

            foreach (var prop in doc.RootElement.EnumerateObject())
                fields.Add(prop.Name);
        }
        catch (System.Text.Json.JsonException)
        {
            // Model binding already validated the body to get this far; on the rare chance it
            // can't be re-parsed here, fall back to "nothing provided" so existing values are
            // preserved rather than risk wiping fields on a read failure.
        }
        return fields;
    }

    // ============================================
    // PATCH /v1/tasks/{id}/status - Transition status
    // ============================================
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> TransitionStatus(int id, [FromBody] TaskStatusUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var current = await _taskService.GetTaskByIdAsync(id, employeeId.Value);
        if (current == null)
            return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));
        if (ConcurrencyHelper.RequireIfMatch(Request, current.UpdatedAt) is { } precondition)
            return StatusCode(precondition.Status, precondition.Body);

        try
        {
            var task = await _taskService.TransitionStatusAsync(id, employeeId.Value, dto.Status, dto.Reason, dto.AssigneeId, dto.ReworkCategory, dto.Outcome);
            var response = await MapToResponseAsync(task, employeeId.Value);
            await AttachLeaveWarningAsync(response, dto.AssigneeId);
            ConcurrencyHelper.SetETag(Response, task.UpdatedAt);
            return Ok(response);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // PATCH /v1/tasks/{id}/assignee - Update assignee
    // ============================================
    [HttpPatch("{id}/assignee")]
    public async Task<IActionResult> UpdateAssignee(int id, [FromBody] TaskAssigneeUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var task = await _taskService.AssignTaskAsync(id, employeeId.Value, dto.AssigneeId);
            var response = await MapToResponseAsync(task, employeeId.Value);
            await AttachLeaveWarningAsync(response, dto.AssigneeId);
            return Ok(response);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // PATCH /v1/tasks/{id}/archive
    // ============================================
    [HttpPatch("{id}/archive")]
    public async Task<IActionResult> SetArchived(int id, [FromBody] TaskArchiveDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var task = await _taskService.SetArchivedAsync(id, employeeId.Value, dto.IsArchived);
            return Ok(await MapToResponseAsync(task, employeeId.Value));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // POST /v1/tasks/{id}/restore - ADMIN only
    // ============================================
    [HttpPost("{id}/restore")]
    [AdminOnly]
    public async Task<IActionResult> RestoreTask(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var restored = await _taskService.RestoreTaskAsync(id, employeeId.Value);
            if (!restored)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // DELETE /v1/tasks/{id} - Soft delete a task
    // ============================================
    [HttpDelete("{id}")]
    [AdminOnly]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _taskService.DeleteTaskAsync(id, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Task not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/tasks/{id}/activity
    // ============================================
    [HttpGet("{id}/activity")]
    public async Task<IActionResult> GetActivity(int id, [FromQuery] int? page = 1, [FromQuery] int? limit = 25, [FromQuery] string? cursor = null)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            // PL-8: cursor pagination alongside the existing page-based scheme - a caller opts in
            // by supplying ?cursor, which takes precedence over page/limit when both are present.
            if (cursor != null || Request.Query.ContainsKey("cursor"))
            {
                var (items, nextCursor) = await _taskService.GetTaskActivityCursorAsync(id, employeeId.Value, cursor, limit ?? 25);
                return Ok(new { data = items.Select(MapActivity), nextCursor });
            }

            var activities = await _taskService.GetTaskActivityAsync(id, employeeId.Value, page, limit);
            return Ok(activities.Select(MapActivity));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    private static TaskActivityResponseDto MapActivity(TaskActivity a) => new()
    {
        Id = a.Id,
        ActorId = a.ActorId,
        ActorName = a.Actor?.FullName ?? string.Empty,
        Action = a.Action,
        Field = a.Field,
        OldValue = a.OldValue,
        NewValue = a.NewValue,
        Reason = a.Reason,
        CreatedAt = a.CreatedAt
    };

    // ============================================
    // GET/POST /v1/tasks/{id}/comments
    // ============================================
    [HttpGet("{id}/comments")]
    public async Task<IActionResult> GetComments(int id, [FromQuery] int? page = 1, [FromQuery] int? limit = 25)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var comments = await _taskService.GetCommentsAsync(id, employeeId.Value, page, limit);
            return Ok(comments.Select(MapCommentToResponse));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] CommentCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var comment = await _taskService.AddCommentAsync(id, employeeId.Value, dto.Body);
            return StatusCode(201, MapCommentToResponse(comment));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // POST/DELETE /v1/tasks/{id}/watchers
    // ============================================
    [HttpPost("{id}/watchers")]
    public async Task<IActionResult> AddWatcher(int id, [FromBody] WatcherAddDto? dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        var targetEmployeeId = dto?.EmployeeId ?? employeeId.Value;

        try
        {
            var watcher = await _taskService.AddWatcherForAsync(id, employeeId.Value, targetEmployeeId);
            return StatusCode(201, new WatcherResponseDto
            {
                EmployeeId = watcher.EmployeeId,
                EmployeeName = watcher.Employee?.FullName ?? string.Empty,
                CreatedAt = watcher.CreatedAt
            });
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpDelete("{id}/watchers/{employeeId}")]
    public async Task<IActionResult> RemoveWatcher(int id, int employeeId)
    {
        var actorEmployeeId = await GetCurrentEmployeeIdAsync();
        if (actorEmployeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var removed = await _taskService.RemoveWatcherForAsync(id, actorEmployeeId.Value, employeeId);
            if (!removed)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Watcher not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET/POST /v1/tasks/{id}/checklist
    // ============================================
    [HttpGet("{id}/checklist")]
    public async Task<IActionResult> GetChecklistItems(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var items = await _taskService.GetChecklistItemsAsync(id, employeeId.Value);
            return Ok(items.Select(MapChecklistToResponse));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id}/checklist")]
    public async Task<IActionResult> AddChecklistItem(int id, [FromBody] ChecklistItemCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var item = await _taskService.AddChecklistItemAsync(id, employeeId.Value, dto.Label, dto.Position);
            return StatusCode(201, MapChecklistToResponse(item));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // PUT /v1/tasks/{id}/tags
    // ============================================
    [HttpPut("{id}/tags")]
    public async Task<IActionResult> ReplaceTags(int id, [FromBody] TaskTagsReplaceDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var tags = await _taskService.ReplaceTaskTagsAsync(id, employeeId.Value, dto.Tags);
            return Ok(tags.Select(t => new TagResponseDto { Id = t.Id, Name = t.Name, Color = t.Color }));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // POST/DELETE /v1/tasks/{id}/dependencies
    // ============================================
    [HttpPost("{id}/dependencies")]
    public async Task<IActionResult> AddDependency(int id, [FromBody] DependencyCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            await _taskService.AddDependencyAsync(id, dto.BlockingTaskId, employeeId.Value);
            return StatusCode(201, null);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpDelete("{id}/dependencies/{blockingTaskId}")]
    public async Task<IActionResult> RemoveDependency(int id, int blockingTaskId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var removed = await _taskService.RemoveDependencyAsync(id, blockingTaskId, employeeId.Value);
            if (!removed)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Dependency not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET/POST /v1/tasks/{id}/time-entries
    // ============================================
    [HttpGet("{id}/time-entries")]
    public async Task<IActionResult> GetTimeEntries(int id, [FromQuery] string? cursor = null, [FromQuery] int? limit = 25)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            // PL-8: cursor pagination alongside the plain list, for tasks with many logged entries.
            if (cursor != null || Request.Query.ContainsKey("cursor"))
            {
                var (items, nextCursor) = await _taskService.GetTimeEntriesCursorAsync(id, employeeId.Value, cursor, limit ?? 25);
                return Ok(new { data = items.Select(MapTimeEntryToResponse), nextCursor });
            }

            var entries = await _taskService.GetTimeEntriesAsync(id, employeeId.Value);
            return Ok(entries.Select(MapTimeEntryToResponse));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id}/time-entries")]
    public async Task<IActionResult> AddTimeEntry(int id, [FromBody] TimeEntryCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var entry = await _taskService.AddTimeEntryAsync(id, employeeId.Value, dto.Minutes, dto.Note, dto.WorkedOn);
            return StatusCode(201, MapTimeEntryToResponse(entry));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET /v1/tasks/{id}/status-intervals (WC-23..WC-27)
    // ============================================
    [HttpGet("{id}/status-intervals")]
    public async Task<IActionResult> GetStatusIntervals(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var intervals = await _taskService.GetStatusIntervalsAsync(id, employeeId.Value);
            var data = intervals.Select(i => new TaskStatusIntervalResponseDto
            {
                Id = i.Id,
                Status = i.Status,
                EnteredAt = i.EnteredAt,
                ExitedAt = i.ExitedAt,
                ActorId = i.ActorId,
                ActorName = i.Actor?.FullName ?? string.Empty,
                DurationSeconds = i.DurationSeconds,
                WorkingHoursDurationSeconds = i.WorkingHoursDurationSeconds,
                IsEstimated = i.IsEstimated
            });
            return Ok(data);
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // GET/POST/PATCH/DELETE /v1/tasks/{id}/acceptance-criteria (WC-10..WC-14)
    // ============================================
    [HttpGet("{id}/acceptance-criteria")]
    public async Task<IActionResult> GetAcceptanceCriteria(int id)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var criteria = await _taskService.GetAcceptanceCriteriaAsync(id, employeeId.Value);
            return Ok(criteria.Select(MapAcceptanceCriterionToResponse));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPost("{id}/acceptance-criteria")]
    public async Task<IActionResult> AddAcceptanceCriterion(int id, [FromBody] AcceptanceCriterionCreateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var criterion = await _taskService.AddAcceptanceCriterionAsync(id, employeeId.Value, dto.Text);
            return StatusCode(201, MapAcceptanceCriterionToResponse(criterion));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpPatch("{id}/acceptance-criteria/{criterionId}")]
    public async Task<IActionResult> UpdateAcceptanceCriterion(int id, int criterionId, [FromBody] AcceptanceCriterionUpdateDto dto)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var criterion = await _taskService.UpdateAcceptanceCriterionAsync(criterionId, employeeId.Value, dto.Text, dto.IsMet);
            return Ok(MapAcceptanceCriterionToResponse(criterion));
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    [HttpDelete("{id}/acceptance-criteria/{criterionId}")]
    public async Task<IActionResult> DeleteAcceptanceCriterion(int id, int criterionId)
    {
        var employeeId = await GetCurrentEmployeeIdAsync();
        if (employeeId == null)
            return NoEmployeeLinkError();

        try
        {
            var deleted = await _taskService.DeleteAcceptanceCriterionAsync(criterionId, employeeId.Value);
            if (!deleted)
                return NotFound(ApiError.Build(TaskErrorCodes.NotFound, "Acceptance criterion not found"));

            return NoContent();
        }
        catch (TaskDomainException ex)
        {
            return StatusCode(ex.HttpStatus, ApiError.Build(ex.Code, ex.Message, ex.Details));
        }
    }

    // ============================================
    // Helper: Map WorkTask to TaskResponseDto
    // ============================================
    private async Task<TaskResponseDto> MapToResponseAsync(WorkTask task, int viewerId)
{
    var isOverdue = task.DueAt.HasValue && task.DueAt.Value < DateTime.UtcNow &&
                    task.Status != "DONE" && task.Status != "CANCELLED";

    List<string> availableTransitions;
    try
    {
        availableTransitions = await _taskService.GetAvailableTransitionsAsync(task, viewerId);
    }
    catch (TaskDomainException)
    {
        availableTransitions = new List<string>();
    }

    var blockedPause = await _slaService.GetBlockedPauseMinutesAsync(task);

    return new TaskResponseDto
    {
        Id = task.Id,
        Key = task.Key,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status,
        Outcome = task.Outcome,                      // ADD THIS
        IsClosureRequired = task.IsClosureRequired,  // ADD THIS
        Priority = task.Priority,
        DepartmentId = task.DepartmentId,
        DepartmentName = task.Department?.Name ?? string.Empty,
        AssigneeId = task.AssigneeId,
        AssigneeName = task.Assignee?.FullName,
        AssigneeLevel = task.Assignee?.Level?.Name,
        CreatedById = task.CreatedById,
        CreatedByName = task.CreatedBy?.FullName ?? string.Empty,
        DueAt = task.DueAt,
        EstimateMinutes = task.EstimateMinutes,
        LoggedMinutes = task.LoggedMinutes,
        IsOverdue = isOverdue,
        ParentTaskId = task.ParentTaskId,
        ParentTaskKey = task.ParentTask?.Key,
        ParentTaskTitle = task.ParentTask?.Title,
        IsArchived = task.IsArchived,
        IsDeleted = task.DeletedAt.HasValue,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        CompletedAt = task.CompletedAt,
        Tags = task.Tags?.Select(tt => tt.Tag?.Name ?? string.Empty).Where(t => !string.IsNullOrEmpty(t)).ToList() ?? new List<string>(),
        WatcherCount = task.Watchers?.Count ?? 0,
        IsWatching = task.Watchers?.Any(w => w.EmployeeId == viewerId) ?? false,
        AvailableTransitions = availableTransitions,
        ReworkCount = task.ReworkCount,
        ReopenCount = task.ReopenCount,
        Checklist = new TaskChecklistSummaryDto
        {
            Total = task.ChecklistItems?.Count ?? 0,
            Done = task.ChecklistItems?.Count(ci => ci.IsDone) ?? 0
        },
        BlockedBy = task.BlockedBy?.Select(b => new TaskDependencySummaryDto
        {
            Id = b.BlockingTaskId,
            Key = b.BlockingTask?.Key ?? string.Empty,
            Title = b.BlockingTask?.Title ?? string.Empty,
            Status = b.BlockingTask?.Status ?? string.Empty
        }).ToList() ?? new List<TaskDependencySummaryDto>(),
        AcceptanceCriteria = new TaskAcceptanceCriteriaSummaryDto
        {
            Total = task.AcceptanceCriteria?.Count ?? 0,
            Met = task.AcceptanceCriteria?.Count(ac => ac.IsMet) ?? 0
        },
        SourceRequestId = task.SourceRequestId,
        BreachState = task.BreachState,
        ResponseTargetAt = task.ResponseTargetAt,
        ResolutionTargetAt = task.ResolutionTargetAt,
        RemainingMinutes = _slaService.GetRemainingMinutes(task, blockedPause),
        BlockedPauseMinutes = blockedPause
    };
}

    // CP-7: warn (do not block) when assignee is on approved leave today, and separately when
    // they'll be on leave by the task's due date - assigning someone work due while they're away
    // is the more useful conflict to surface than just "on leave right now".
    private async Task AttachLeaveWarningAsync(TaskResponseDto response, int? assigneeId)
    {
        if (!assigneeId.HasValue) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await _leaveService.IsOnApprovedLeaveAsync(assigneeId.Value, today))
        {
            response.Warnings ??= new List<string>();
            response.Warnings.Add("Assignee is on approved leave today");
        }

        if (response.DueAt.HasValue)
        {
            var dueDate = DateOnly.FromDateTime(response.DueAt.Value);
            if (dueDate != today && await _leaveService.IsOnApprovedLeaveAsync(assigneeId.Value, dueDate))
            {
                response.Warnings ??= new List<string>();
                response.Warnings.Add("Assignee will be on approved leave on the task's due date");
            }
        }
    }

    // CM-3: soft-deleted comments come back as tombstones instead of being dropped.
    private static CommentResponseDto MapCommentToResponse(TaskComment c)
    {
        var isDeleted = c.DeletedAt.HasValue;
        return new CommentResponseDto
        {
            Id = c.Id,
            TaskId = c.TaskId,
            Body = isDeleted ? "[comment deleted]" : c.Body,
            AuthorId = c.AuthorId,
            AuthorName = isDeleted ? "[deleted]" : (c.Author?.FullName ?? string.Empty),
            AuthorLevel = isDeleted ? string.Empty : (c.Author?.Level?.Name ?? string.Empty),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            IsDeleted = isDeleted
        };
    }

    private static ChecklistItemResponseDto MapChecklistToResponse(TaskChecklistItem item)
    {
        return new ChecklistItemResponseDto
        {
            Id = item.Id,
            TaskId = item.TaskId,
            Label = item.Label,
            IsDone = item.IsDone,
            Position = item.Position,
            CompletedAt = item.CompletedAt,
            CompletedById = item.CompletedById,
            CompletedByName = item.CompletedBy?.FullName
        };
    }

    private static AcceptanceCriterionResponseDto MapAcceptanceCriterionToResponse(TaskAcceptanceCriterion criterion)
    {
        return new AcceptanceCriterionResponseDto
        {
            Id = criterion.Id,
            TaskId = criterion.TaskId,
            Text = criterion.Text,
            IsMet = criterion.IsMet,
            MetById = criterion.MetById,
            MetByName = criterion.MetBy?.FullName,
            MetAt = criterion.MetAt,
            Position = criterion.Position
        };
    }

    private static TimeEntryResponseDto MapTimeEntryToResponse(TaskTimeEntry entry)
    {
        return new TimeEntryResponseDto
        {
            Id = entry.Id,
            TaskId = entry.TaskId,
            EmployeeId = entry.EmployeeId,
            EmployeeName = entry.Employee?.FullName ?? string.Empty,
            Minutes = entry.Minutes,
            Note = entry.Note,
            WorkedOn = entry.WorkedOn,
            CreatedAt = entry.CreatedAt
        };
    }
}

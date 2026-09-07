using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;
    private readonly IWorkingCalendarService _calendarService;
    private readonly Random _random = new Random();

    public TaskRepository(AppDbContext context, IWorkingCalendarService calendarService)
    {
        _context = context;
        _calendarService = calendarService;
    }

    // ============================================
    // CRUD Operations
    // ============================================
    
    public async Task<WorkTask?> GetByIdAsync(int id)
    {
        return await _context.Tasks
            .Include(t => t.Department)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Include(t => t.ParentTask)
            .Include(t => t.Tags)
                .ThenInclude(tt => tt.Tag)
            .Include(t => t.Watchers)
            .Include(t => t.ChecklistItems)
            .Include(t => t.AcceptanceCriteria)
            .FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null);
    }

    public async Task<WorkTask?> GetByKeyAsync(string key)
    {
        return await _context.Tasks
            .Include(t => t.Department)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Key == key && t.DeletedAt == null);
    }

    public async Task<WorkTask> CreateAsync(WorkTask task)
    {
        // Generate unique key
        task.Key = await GenerateTaskKeyAsync();
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<WorkTask> UpdateAsync(WorkTask task)
    {
        task.UpdatedAt = DateTime.UtcNow;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var task = await _context.Tasks.FindAsync(id);
        if (task == null) return false;
        
        task.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var task = await _context.Tasks.FindAsync(id);
        if (task == null || task.DeletedAt == null) return false;
        
        task.DeletedAt = null;
        await _context.SaveChangesAsync();
        return true;
    }

    // ============================================
    // Query with filters
    // ============================================
    
    public async Task<(IEnumerable<WorkTask> Items, int TotalCount)> GetFilteredAsync(
        int viewerId,
        int? viewerDepartmentId,
        bool isAdmin,
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
        var query = _context.Tasks
            .Include(t => t.Department)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .Include(t => t.Tags)
                .ThenInclude(tt => tt.Tag)
            .Include(t => t.Watchers)
            .Include(t => t.ChecklistItems)
            .Include(t => t.AcceptanceCriteria)
            .Where(t => t.DeletedAt == null);

        // LS-1: visibility filter applied as part of the SAME queryable, BEFORE Skip/Take, so
        // pagination and TotalCount reflect only rows the viewer can actually read.
        if (!isAdmin)
        {
            query = query.Where(t =>
                t.DepartmentId == viewerDepartmentId ||
                t.CreatedById == viewerId ||
                t.AssigneeId == viewerId ||
                t.Watchers.Any(w => w.EmployeeId == viewerId));
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)) ||
                t.Key.ToLower().Contains(term) ||
                (t.Assignee != null && t.Assignee.FullName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = status.Split(',').Select(s => s.Trim());
            query = query.Where(t => statuses.Contains(t.Status));
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            var priorities = priority.Split(',').Select(p => p.Trim());
            query = query.Where(t => priorities.Contains(t.Priority));
        }

        if (assigneeId.HasValue)
        {
            query = query.Where(t => t.AssigneeId == assigneeId.Value);
        }

        if (assigneeIsNull.HasValue && assigneeIsNull.Value)
        {
            query = query.Where(t => t.AssigneeId == null);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(t => t.DepartmentId == departmentId.Value);
        }

        if (createdById.HasValue)
        {
            query = query.Where(t => t.CreatedById == createdById.Value);
        }

        if (watchedByMe.HasValue && watchedByMe.Value)
        {
            query = query.Where(t => t.Watchers.Any(w => w.EmployeeId == viewerId));
        }

        if (!string.IsNullOrWhiteSpace(tags))
        {
            var tagNames = tags.Split(',').Select(t => t.Trim().ToLower()).ToList();
            query = query.Where(t => t.Tags.Any(tt => tagNames.Contains(tt.Tag.Name.ToLower())));
        }

        if (dueBefore.HasValue)
        {
            query = query.Where(t => t.DueAt.HasValue && t.DueAt.Value <= dueBefore.Value);
        }

        if (dueAfter.HasValue)
        {
            query = query.Where(t => t.DueAt.HasValue && t.DueAt.Value >= dueAfter.Value);
        }

        if (overdue.HasValue && overdue.Value)
        {
            var now = DateTime.UtcNow;
            query = query.Where(t => t.DueAt.HasValue && t.DueAt.Value < now && t.Status != "DONE" && t.Status != "CANCELLED");
        }

        if (!includeArchived.HasValue || !includeArchived.Value)
        {
            query = query.Where(t => !t.IsArchived);
        }

        if (parentTaskId.HasValue)
        {
            if (parentTaskId.Value == 0)
            {
                // No parent (top-level tasks)
                query = query.Where(t => t.ParentTaskId == null);
            }
            else
            {
                query = query.Where(t => t.ParentTaskId == parentTaskId.Value);
            }
        }

        // Sorting
        query = ApplySorting(query, sort ?? "-createdAt");

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Pagination
        if (page.HasValue && limit.HasValue)
        {
            var skip = (page.Value - 1) * limit.Value;
            query = query.Skip(skip).Take(limit.Value);
        }

        var items = await query.ToListAsync();
        return (items, totalCount);
    }

    private IQueryable<WorkTask> ApplySorting(IQueryable<WorkTask> query, string sort)
    {
        // Supports a comma-separated list of sort keys, e.g. "priority,dueAt" (LS-6: priority severity
        // then DueAt ascending, nulls last).
        var parts = sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) parts = new[] { "-createdAt" };

        IOrderedQueryable<WorkTask>? ordered = null;

        foreach (var part in parts)
        {
            var descending = part.StartsWith("-");
            var field = (descending ? part.Substring(1) : part).ToLower();

            switch (field)
            {
                case "createdat":
                    ordered = Then(ordered, query, t => t.CreatedAt, descending);
                    break;
                case "updatedat":
                    ordered = Then(ordered, query, t => t.UpdatedAt, descending);
                    break;
                case "duedate":
                case "dueat":
                    // Nulls last regardless of direction.
                    ordered = Then(ordered, query, t => t.DueAt.HasValue ? 0 : 1, false);
                    ordered = descending ? ordered.ThenByDescending(t => t.DueAt) : ordered.ThenBy(t => t.DueAt);
                    break;
                case "priority":
                    // LS-2: business severity, not alphabetical - URGENT(0) > HIGH(1) > NORMAL(2) > LOW(3).
                    ordered = Then(ordered, query, t => t.Priority == "URGENT" ? 0 : t.Priority == "HIGH" ? 1 : t.Priority == "NORMAL" ? 2 : 3, descending);
                    break;
                case "status":
                    ordered = Then(ordered, query, t => t.Status, descending);
                    break;
                case "title":
                    ordered = Then(ordered, query, t => t.Title, descending);
                    break;
                default:
                    ordered = Then(ordered, query, t => t.CreatedAt, true);
                    break;
            }
        }

        return ordered ?? query.OrderByDescending(t => t.CreatedAt);
    }

    private static IOrderedQueryable<WorkTask> Then<TKey>(
        IOrderedQueryable<WorkTask>? ordered, IQueryable<WorkTask> query,
        System.Linq.Expressions.Expression<Func<WorkTask, TKey>> keySelector, bool descending)
    {
        if (ordered == null)
            return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);

        return descending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
    }

    // ============================================
    // Helper: Generate Task Key
    // ============================================
    
    private async Task<string> GenerateTaskKeyAsync()
    {
        // Get the last task ID to generate the next key
        var lastTask = await _context.Tasks
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();
        
        var nextId = (lastTask?.Id ?? 0) + 1;
        return $"TSK-{nextId}";
    }

    // ============================================
    // Assignment
    // ============================================
    
    public async Task<WorkTask> AssignTaskAsync(int taskId, int? assigneeId)
    {
        var task = await GetByIdAsync(taskId);
        if (task == null) throw new ArgumentException("Task not found");

        task.AssigneeId = assigneeId;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return task;
    }

    // ============================================
    // Status transition
    // ============================================
    
    public async Task<WorkTask> UpdateStatusAsync(int taskId, string status, string? reason, int? reworkCount = null, int? reopenCount = null)
{
    var task = await GetByIdAsync(taskId);
    if (task == null) throw new ArgumentException("Task not found");

    // If completing task, set CompletedAt
    if (status == "DONE" && task.Status != "DONE")
    {
        task.CompletedAt = DateTime.UtcNow;
    }
    else if (task.Status == "DONE" && status != "DONE")
    {
        task.CompletedAt = null; // Reopened
    }

    task.Status = status;
    task.UpdatedAt = DateTime.UtcNow;
    
    // Update rework/reopen counts if provided
    if (reworkCount.HasValue)
        task.ReworkCount = reworkCount.Value;
    if (reopenCount.HasValue)
        task.ReopenCount = reopenCount.Value;
    
    await _context.SaveChangesAsync();
    return task;
}
    // ============================================
    // Checklists
    // ============================================
    
    public async Task<TaskChecklistItem> AddChecklistItemAsync(int taskId, string label, int position)
    {
        var item = new TaskChecklistItem
        {
            TaskId = taskId,
            Label = label,
            Position = position,
            IsDone = false
        };
        _context.TaskChecklistItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<TaskChecklistItem> UpdateChecklistItemAsync(int itemId, string? label, bool? isDone, int? position, int? completedById = null)
    {
        var item = await _context.TaskChecklistItems.FindAsync(itemId);
        if (item == null) throw new ArgumentException("Checklist item not found");

        if (label != null) item.Label = label;
        if (isDone.HasValue)
        {
            item.IsDone = isDone.Value;
            if (isDone.Value)
            {
                item.CompletedAt = DateTime.UtcNow;
                item.CompletedById = completedById; // TR-40: record who completed it, not just when.
            }
            else
            {
                item.CompletedAt = null;
                item.CompletedById = null;
            }
        }
        if (position.HasValue) item.Position = position.Value;

        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteChecklistItemAsync(int itemId)
    {
        var item = await _context.TaskChecklistItems.FindAsync(itemId);
        if (item == null) return false;
        
        _context.TaskChecklistItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<TaskChecklistItem>> GetChecklistItemsAsync(int taskId)
    {
        return await _context.TaskChecklistItems
            .Where(ci => ci.TaskId == taskId)
            .OrderBy(ci => ci.Position)
            .ToListAsync();
    }

    // ============================================
    // Dependencies
    // ============================================
    
    public async Task<TaskDependency> AddDependencyAsync(int blockedTaskId, int blockingTaskId)
    {
        var dependency = new TaskDependency
        {
            BlockedTaskId = blockedTaskId,
            BlockingTaskId = blockingTaskId,
            CreatedAt = DateTime.UtcNow
        };
        _context.TaskDependencies.Add(dependency);
        await _context.SaveChangesAsync();
        return dependency;
    }

    public async Task<bool> RemoveDependencyAsync(int blockedTaskId, int blockingTaskId)
    {
        var dependency = await _context.TaskDependencies
            .FirstOrDefaultAsync(d => d.BlockedTaskId == blockedTaskId && d.BlockingTaskId == blockingTaskId);
        if (dependency == null) return false;
        
        _context.TaskDependencies.Remove(dependency);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<TaskDependency>> GetBlockedByTasksAsync(int taskId)
    {
        return await _context.TaskDependencies
            .Include(d => d.BlockingTask)
            .Where(d => d.BlockedTaskId == taskId)
            .ToListAsync();
    }

    public async Task<IEnumerable<TaskDependency>> GetBlockingTasksAsync(int taskId)
    {
        return await _context.TaskDependencies
            .Include(d => d.BlockedTask)
            .Where(d => d.BlockingTaskId == taskId)
            .ToListAsync();
    }

    // ============================================
    // Time Entries
    // ============================================
    
    public async Task<TaskTimeEntry> AddTimeEntryAsync(int taskId, int employeeId, int minutes, string? note, DateTime workedOn)
    {
        var entry = new TaskTimeEntry
        {
            TaskId = taskId,
            EmployeeId = employeeId,
            Minutes = minutes,
            Note = note,
            WorkedOn = workedOn,
            CreatedAt = DateTime.UtcNow
        };
        _context.TaskTimeEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<IEnumerable<TaskTimeEntry>> GetTimeEntriesAsync(int taskId)
    {
        return await _context.TaskTimeEntries
            .Include(te => te.Employee)
            .Where(te => te.TaskId == taskId)
            .OrderByDescending(te => te.WorkedOn)
            .ToListAsync();
    }

    // PL-8: cursor pagination alongside the unbounded list above.
    public async Task<(IEnumerable<TaskTimeEntry> Items, string? NextCursor)> GetTimeEntriesCursorAsync(
        int taskId, string? cursor, int limit)
    {
        var query = _context.TaskTimeEntries
            .Include(te => te.Employee)
            .Where(te => te.TaskId == taskId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(cursor) && int.TryParse(cursor, out var cursorId))
            query = query.Where(te => te.Id < cursorId);

        var items = await query.OrderByDescending(te => te.Id).Take(limit + 1).ToListAsync();
        var nextCursor = items.Count > limit ? items.Last().Id.ToString() : null;
        return (items.Take(limit).ToList(), nextCursor);
    }

    public async Task<bool> DeleteTimeEntryAsync(int timeEntryId, int employeeId)
    {
        var entry = await _context.TaskTimeEntries
            .Include(te => te.Timesheet)
            .FirstOrDefaultAsync(te => te.Id == timeEntryId && te.EmployeeId == employeeId);
        if (entry == null) return false;

        if (entry.Timesheet != null && entry.Timesheet.State is TimesheetStates.Approved or TimesheetStates.Submitted)
            throw new StaffDesk.Core.Exceptions.TaskDomainException(
                StaffDesk.Core.Exceptions.TaskErrorCodes.ValidationError,
                "Cannot edit time entries on a SUBMITTED or APPROVED timesheet (CP-15).",
                409);

        // Also block if week is approved/submitted but entry wasn't linked (fallback by WorkedOn)
        if (entry.TimesheetId == null)
        {
            var weekStart = TimesheetRepository.MondayOf(DateOnly.FromDateTime(entry.WorkedOn.ToUniversalTime()));
            var locked = await _context.Timesheets.AnyAsync(t =>
                t.EmployeeId == employeeId
                && t.WeekStart == weekStart
                && (t.State == TimesheetStates.Approved || t.State == TimesheetStates.Submitted));
            if (locked)
                throw new StaffDesk.Core.Exceptions.TaskDomainException(
                    StaffDesk.Core.Exceptions.TaskErrorCodes.ValidationError,
                    "Cannot edit time entries on a SUBMITTED or APPROVED timesheet (CP-15).",
                    409);
        }

        _context.TaskTimeEntries.Remove(entry);
        await _context.SaveChangesAsync();
        return true;
    }

    // ============================================
    // Activity Log
    // ============================================
    
    public async Task<TaskActivity> AddActivityAsync(int taskId, int actorId, string action, string? field, string? oldValue, string? newValue, string? reason, string? correlationId)
    {
        var activity = new TaskActivity
        {
            TaskId = taskId,
            ActorId = actorId,
            Action = action,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };
        _context.TaskActivities.Add(activity);
        await _context.SaveChangesAsync();
        return activity;
    }

    public async Task<IEnumerable<TaskActivity>> GetActivitiesAsync(int taskId, int? page = null, int? limit = null)
    {
        var query = _context.TaskActivities
            .Include(a => a.Actor)
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.CreatedAt);

        if (page.HasValue && limit.HasValue)
        {
            var skip = (page.Value - 1) * limit.Value;
            query = (IOrderedQueryable<TaskActivity>)query.Skip(skip).Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    // PL-8: cursor pagination alongside the page-based scheme above, for high-volume callers.
    public async Task<(IEnumerable<TaskActivity> Items, string? NextCursor)> GetActivitiesCursorAsync(
        int taskId, string? cursor, int limit)
    {
        var query = _context.TaskActivities
            .Include(a => a.Actor)
            .Where(a => a.TaskId == taskId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(cursor) && int.TryParse(cursor, out var cursorId))
            query = query.Where(a => a.Id < cursorId);

        var items = await query.OrderByDescending(a => a.Id).Take(limit + 1).ToListAsync();
        var nextCursor = items.Count > limit ? items.Last().Id.ToString() : null;
        return (items.Take(limit).ToList(), nextCursor);
    }

    // ============================================
    // Watchers
    // ============================================
    
    public async Task<TaskWatcher> AddWatcherAsync(int taskId, int employeeId)
    {
        // Idempotent: several callers add the creator/assignee/requester as a watcher without first
        // checking whether they already are one (e.g. assigning a task to someone who became a
        // watcher earlier, such as the original requester of an accepted TaskRequest) - a second
        // Add here previously threw a duplicate-key exception on the (TaskId, EmployeeId) PK.
        var existing = await _context.TaskWatchers
            .FirstOrDefaultAsync(w => w.TaskId == taskId && w.EmployeeId == employeeId);
        if (existing != null)
            return existing;

        var watcher = new TaskWatcher
        {
            TaskId = taskId,
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow
        };
        _context.TaskWatchers.Add(watcher);
        await _context.SaveChangesAsync();
        return watcher;
    }

    public async Task<bool> RemoveWatcherAsync(int taskId, int employeeId)
    {
        var watcher = await _context.TaskWatchers
            .FirstOrDefaultAsync(w => w.TaskId == taskId && w.EmployeeId == employeeId);
        if (watcher == null) return false;
        
        _context.TaskWatchers.Remove(watcher);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<TaskWatcher>> GetWatchersAsync(int taskId)
    {
        return await _context.TaskWatchers
            .Include(w => w.Employee)
            .Where(w => w.TaskId == taskId)
            .ToListAsync();
    }

    public async Task<bool> IsWatchingAsync(int taskId, int employeeId)
    {
        return await _context.TaskWatchers
            .AnyAsync(w => w.TaskId == taskId && w.EmployeeId == employeeId);
    }

    // ============================================
    // Tags
    // ============================================
    
    public async Task<Tag> GetOrCreateTagAsync(string name)
    {
        var normalizedName = name.Trim().ToLower();
        var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedName);
        if (tag == null)
        {
            tag = new Tag { Name = name.Trim() };
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
        }
        return tag;
    }

    public async Task<Tag?> FindTagByNameAsync(string name)
    {
        var normalizedName = name.Trim().ToLower();
        return await _context.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedName);
    }

    public async Task<bool> AddTagToTaskAsync(int taskId, int tagId)
    {
        var exists = await _context.TaskTags
            .AnyAsync(tt => tt.TaskId == taskId && tt.TagId == tagId);
        if (exists) return false;

        _context.TaskTags.Add(new TaskTag { TaskId = taskId, TagId = tagId });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveTagFromTaskAsync(int taskId, int tagId)
    {
        var taskTag = await _context.TaskTags
            .FirstOrDefaultAsync(tt => tt.TaskId == taskId && tt.TagId == tagId);
        if (taskTag == null) return false;
        
        _context.TaskTags.Remove(taskTag);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Tag>> GetTagsForTaskAsync(int taskId)
    {
        return await _context.TaskTags
            .Include(tt => tt.Tag)
            .Where(tt => tt.TaskId == taskId)
            .Select(tt => tt.Tag)
            .ToListAsync();
    }

    public async Task<IEnumerable<Tag>> GetAllTagsAsync()
    {
        return await _context.Tags
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<bool> DeleteTagAsync(int tagId)
    {
        var tag = await _context.Tags.FindAsync(tagId);
        if (tag == null) return false;

        // TaskTag -> Tag FK is Restrict, so detach from all tasks first.
        var taskTags = await _context.TaskTags.Where(tt => tt.TagId == tagId).ToListAsync();
        _context.TaskTags.RemoveRange(taskTags);

        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<(Tag Tag, int Count)>> GetTagsWithUsageCountsAsync()
    {
        var data = await _context.Tags
            .Select(t => new { Tag = t, Count = t.Tasks.Count })
            .OrderBy(x => x.Tag.Name)
            .ToListAsync();

        return data.Select(x => (x.Tag, x.Count)).ToList();
    }

    // ============================================
    // Department Reporting (RP-1, RP-2)
    // ============================================

    public async Task<(Dictionary<string, int> StatusCounts, int Overdue, int Unassigned)> GetDepartmentTaskSummaryAsync(int departmentId)
    {
        var now = DateTime.UtcNow;

        var statusCounts = await _context.Tasks
            .Where(t => t.DepartmentId == departmentId && t.DeletedAt == null)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var overdue = await _context.Tasks.CountAsync(t =>
            t.DepartmentId == departmentId && t.DeletedAt == null &&
            t.DueAt.HasValue && t.DueAt.Value < now && t.Status != "DONE" && t.Status != "CANCELLED");

        var unassigned = await _context.Tasks.CountAsync(t =>
            t.DepartmentId == departmentId && t.DeletedAt == null && t.AssigneeId == null);

        return (statusCounts.ToDictionary(x => x.Status, x => x.Count), overdue, unassigned);
    }

    public async Task<List<(int EmployeeId, string EmployeeName, Dictionary<string, int> PriorityCounts)>> GetDepartmentWorkloadAsync(int departmentId)
    {
        var activeStatuses = new[] { "OPEN", "IN_PROGRESS", "BLOCKED", "IN_REVIEW" };

        var employees = await _context.Employees
            .Where(e => e.DepartmentId == departmentId && e.IsActive)
            .Select(e => new { e.Id, e.FullName })
            .ToListAsync();

        var raw = await _context.Tasks
            .Where(t => t.DepartmentId == departmentId && t.DeletedAt == null &&
                        t.AssigneeId != null && activeStatuses.Contains(t.Status))
            .GroupBy(t => new { t.AssigneeId, t.Priority })
            .Select(g => new { g.Key.AssigneeId, g.Key.Priority, Count = g.Count() })
            .ToListAsync();

        var result = new List<(int, string, Dictionary<string, int>)>();
        foreach (var e in employees)
        {
            var priorityCounts = raw
                .Where(r => r.AssigneeId == e.Id)
                .ToDictionary(r => r.Priority, r => r.Count);
            result.Add((e.Id, e.FullName, priorityCounts));
        }

        return result;
    }

    // ============================================
    // Notifications
    // ============================================
    
    public async Task<Notification> CreateNotificationAsync(int recipientId, string type, string message, int? taskId = null)
    {
        var notification = new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Message = message,
            TaskId = taskId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task<IEnumerable<Notification>> GetNotificationsAsync(int recipientId, bool? unreadOnly = null, int? page = null, int? limit = null)
    {
        var query = _context.Notifications
            .Include(n => n.Task)
            .Where(n => n.RecipientId == recipientId);

        if (unreadOnly.HasValue && unreadOnly.Value)
        {
            query = query.Where(n => !n.IsRead);
        }

        query = query.OrderByDescending(n => n.CreatedAt);

        if (page.HasValue && limit.HasValue)
        {
            var skip = (page.Value - 1) * limit.Value;
            query = query.Skip(skip).Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    // PL-8: cursor pagination alongside the page-based scheme above, for high-volume callers.
    public async Task<(IEnumerable<Notification> Items, string? NextCursor)> GetNotificationsCursorAsync(
        int recipientId, bool? unreadOnly, string? cursor, int limit)
    {
        var query = _context.Notifications
            .Include(n => n.Task)
            .Where(n => n.RecipientId == recipientId)
            .AsQueryable();

        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);

        if (!string.IsNullOrWhiteSpace(cursor) && int.TryParse(cursor, out var cursorId))
            query = query.Where(n => n.Id < cursorId);

        var items = await query.OrderByDescending(n => n.Id).Take(limit + 1).ToListAsync();
        var nextCursor = items.Count > limit ? items.Last().Id.ToString() : null;
        return (items.Take(limit).ToList(), nextCursor);
    }

    public async Task<int> GetUnreadCountAsync(int recipientId)
    {
        return await _context.Notifications
            .CountAsync(n => n.RecipientId == recipientId && !n.IsRead);
    }

    public async Task<bool> MarkNotificationReadAsync(int notificationId, int recipientId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == recipientId);
        if (notification == null) return false;
        
        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkAllNotificationsReadAsync(int recipientId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .ToListAsync();
        
        foreach (var n in notifications)
        {
            n.IsRead = true;
        }
        await _context.SaveChangesAsync();
        return true;
    }

    // ============================================
// Comments
// ============================================
public async Task<TaskComment> AddCommentAsync(TaskComment comment)
{
    comment.CreatedAt = DateTime.UtcNow;
    _context.TaskComments.Add(comment);
    await _context.SaveChangesAsync();
    return comment;
}

public async Task<TaskComment?> GetCommentByIdAsync(int commentId)
{
    return await _context.TaskComments
        .Include(c => c.Author)
        .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null);
}

public async Task<TaskComment> UpdateCommentAsync(TaskComment comment)
{
    comment.UpdatedAt = DateTime.UtcNow;
    _context.TaskComments.Update(comment);
    await _context.SaveChangesAsync();
    return comment;
}

public async Task<bool> DeleteCommentAsync(int commentId)
{
    var comment = await _context.TaskComments.FindAsync(commentId);
    if (comment == null) return false;
    comment.DeletedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
    return true;
}

public async Task<IEnumerable<TaskComment>> GetCommentsByTaskIdAsync(int taskId, int? page = null, int? limit = null)
{
    // CM-3: include soft-deleted comments so the caller can render a tombstone instead of
    // silently dropping them from the thread; the controller/DTO mapping hides the real body.
    IQueryable<TaskComment> query = _context.TaskComments
        .Include(c => c.Author)
        .Where(c => c.TaskId == taskId)
        .OrderBy(c => c.CreatedAt);

    if (page.HasValue && limit.HasValue)
    {
        var skip = (page.Value - 1) * limit.Value;
        query = query.Skip(skip).Take(limit.Value);
    }

    return await query.ToListAsync();
}

public async Task<TaskChecklistItem?> GetChecklistItemByIdAsync(int itemId)
{
    return await _context.TaskChecklistItems
        .FirstOrDefaultAsync(ci => ci.Id == itemId);
}

// ============================================
// Subtasks / Transactions
// ============================================

public async Task<IEnumerable<WorkTask>> GetSubtasksAsync(int parentTaskId)
{
    return await _context.Tasks
        .Where(t => t.ParentTaskId == parentTaskId && t.DeletedAt == null)
        .ToListAsync();
}

public async Task ExecuteInTransactionAsync(Func<Task> operation)
{
    var strategy = _context.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await operation();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    });
}

// ============================================
// Part B - Status Duration Tracking (WC-23..WC-27)
// ============================================

public async Task<TaskStatusInterval> OpenStatusIntervalAsync(int taskId, string status, int actorId, DateTime enteredAt)
{
    var interval = new TaskStatusInterval
    {
        TaskId = taskId,
        Status = status,
        EnteredAt = enteredAt,
        ActorId = actorId,
        IsEstimated = false
    };
    _context.TaskStatusIntervals.Add(interval);
    await _context.SaveChangesAsync();
    return interval;
}

public async Task CloseOpenStatusIntervalAsync(int taskId, DateTime exitedAt)
{
    var open = await _context.TaskStatusIntervals
        .Where(i => i.TaskId == taskId && i.ExitedAt == null)
        .OrderByDescending(i => i.EnteredAt)
        .FirstOrDefaultAsync();
    if (open == null) return;

    var task = await _context.Tasks.FindAsync(taskId);
    if (task == null) return;

    open.ExitedAt = exitedAt;
    open.DurationSeconds = (int)Math.Max(0, (exitedAt - open.EnteredAt).TotalSeconds);

    // WC-24: working-hours duration against the organisation calendar (§6.1 stub).
    var workingMinutes = _calendarService.GetWorkingMinutes(open.EnteredAt, exitedAt, task.DepartmentId);
    open.WorkingHoursDurationSeconds = (int)(workingMinutes * 60);

    // SL-3: accumulate BLOCKED pause separately on the task.
    if (open.Status == "BLOCKED")
    {
        task.BlockedPauseMinutes += (int)workingMinutes;
        task.UpdatedAt = DateTime.UtcNow;
    }

    await _context.SaveChangesAsync();
}

public async Task<IEnumerable<TaskStatusInterval>> GetStatusIntervalsAsync(int taskId)
{
    return await _context.TaskStatusIntervals
        .Include(i => i.Actor)
        .Where(i => i.TaskId == taskId)
        .OrderBy(i => i.EnteredAt)
        .ToListAsync();
}

// ============================================
// Part B - Acceptance Criteria / Definition of Done (WC-10..WC-14)
// ============================================

public async Task<TaskAcceptanceCriterion> AddAcceptanceCriterionAsync(int taskId, string text, int position)
{
    var criterion = new TaskAcceptanceCriterion
    {
        TaskId = taskId,
        Text = text,
        Position = position,
        IsMet = false,
        CreatedAt = DateTime.UtcNow
    };
    _context.TaskAcceptanceCriteria.Add(criterion);
    await _context.SaveChangesAsync();
    return criterion;
}

public async Task<TaskAcceptanceCriterion?> GetAcceptanceCriterionByIdAsync(int id)
{
    return await _context.TaskAcceptanceCriteria
        .Include(ac => ac.MetBy)
        .FirstOrDefaultAsync(ac => ac.Id == id);
}

public async Task<TaskAcceptanceCriterion> UpdateAcceptanceCriterionAsync(int id, string? text, bool? isMet, int? metById)
{
    var criterion = await _context.TaskAcceptanceCriteria.FindAsync(id);
    if (criterion == null) throw new ArgumentException("Acceptance criterion not found");

    if (text != null) criterion.Text = text;
    if (isMet.HasValue)
    {
        criterion.IsMet = isMet.Value;
        if (isMet.Value)
        {
            criterion.MetAt = DateTime.UtcNow;
            criterion.MetById = metById;
        }
        else
        {
            criterion.MetAt = null;
            criterion.MetById = null;
        }
    }

    await _context.SaveChangesAsync();
    return criterion;
}

public async Task<bool> DeleteAcceptanceCriterionAsync(int id)
{
    var criterion = await _context.TaskAcceptanceCriteria.FindAsync(id);
    if (criterion == null) return false;

    _context.TaskAcceptanceCriteria.Remove(criterion);
    await _context.SaveChangesAsync();
    return true;
}

public async Task<IEnumerable<TaskAcceptanceCriterion>> GetAcceptanceCriteriaAsync(int taskId)
{
    return await _context.TaskAcceptanceCriteria
        .Include(ac => ac.MetBy)
        .Where(ac => ac.TaskId == taskId)
        .OrderBy(ac => ac.Position)
        .ToListAsync();
}

public async Task CopyDepartmentDefaultCriteriaAsync(int taskId, int departmentId)
{
    var defaults = await _context.DepartmentDefaultCriteria
        .Where(d => d.DepartmentId == departmentId)
        .OrderBy(d => d.Position)
        .ToListAsync();

    if (!defaults.Any()) return;

    foreach (var d in defaults)
    {
        _context.TaskAcceptanceCriteria.Add(new TaskAcceptanceCriterion
        {
            TaskId = taskId,
            Text = d.Text,
            Position = d.Position,
            IsMet = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    await _context.SaveChangesAsync();
}

public async Task<DepartmentDefaultCriterion> AddDepartmentDefaultCriterionAsync(int departmentId, string text, int position)
{
    var criterion = new DepartmentDefaultCriterion
    {
        DepartmentId = departmentId,
        Text = text,
        Position = position,
        CreatedAt = DateTime.UtcNow
    };
    _context.DepartmentDefaultCriteria.Add(criterion);
    await _context.SaveChangesAsync();
    return criterion;
}

public async Task<DepartmentDefaultCriterion?> GetDepartmentDefaultCriterionByIdAsync(int id)
{
    return await _context.DepartmentDefaultCriteria.FindAsync(id);
}

public async Task<bool> DeleteDepartmentDefaultCriterionAsync(int id)
{
    var criterion = await _context.DepartmentDefaultCriteria.FindAsync(id);
    if (criterion == null) return false;

    _context.DepartmentDefaultCriteria.Remove(criterion);
    await _context.SaveChangesAsync();
    return true;
}

public async Task<IEnumerable<DepartmentDefaultCriterion>> GetDepartmentDefaultCriteriaAsync(int departmentId)
{
    return await _context.DepartmentDefaultCriteria
        .Where(d => d.DepartmentId == departmentId)
        .OrderBy(d => d.Position)
        .ToListAsync();
}
}
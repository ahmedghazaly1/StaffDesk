using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ITaskRepository
{
    // CRUD Operations
    Task<WorkTask?> GetByIdAsync(int id);
    Task<WorkTask?> GetByKeyAsync(string key);
    Task<WorkTask> CreateAsync(WorkTask task);
    Task<WorkTask> UpdateAsync(WorkTask task);
    Task<bool> DeleteAsync(int id); // Soft delete
    Task<bool> RestoreAsync(int id); // Restore soft-deleted task
    
    // Query with filters
    // viewerId/viewerDepartmentId/isAdmin drive the row-level visibility predicate, which MUST be
    // applied as part of the same IQueryable (before Skip/Take) so pagination and TotalCount are correct.
    Task<(IEnumerable<WorkTask> Items, int TotalCount)> GetFilteredAsync(
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
        bool? assigneeIsNull = null
    );

    Task<IEnumerable<WorkTask>> GetSubtasksAsync(int parentTaskId);

    // Wraps the given operation in a single DB transaction (with EF Core's execution strategy).
    Task ExecuteInTransactionAsync(Func<Task> operation);

    // Assignment
    Task<WorkTask> AssignTaskAsync(int taskId, int? assigneeId);
    
    // Status transition
    Task<WorkTask> UpdateStatusAsync(int taskId, string status, string? reason, int? reworkCount = null, int? reopenCount = null);
    
    // Checklists
    Task<TaskChecklistItem> AddChecklistItemAsync(int taskId, string label, int position);
    Task<TaskChecklistItem> UpdateChecklistItemAsync(int itemId, string? label, bool? isDone, int? position, int? completedById = null);
    Task<bool> DeleteChecklistItemAsync(int itemId);
    Task<IEnumerable<TaskChecklistItem>> GetChecklistItemsAsync(int taskId);
    
    // Dependencies
    Task<TaskDependency> AddDependencyAsync(int blockedTaskId, int blockingTaskId);
    Task<bool> RemoveDependencyAsync(int blockedTaskId, int blockingTaskId);
    Task<IEnumerable<TaskDependency>> GetBlockedByTasksAsync(int taskId);
    Task<IEnumerable<TaskDependency>> GetBlockingTasksAsync(int taskId);
    
    // Time Entries
    Task<TaskTimeEntry> AddTimeEntryAsync(int taskId, int employeeId, int minutes, string? note, DateTime workedOn);
    Task<IEnumerable<TaskTimeEntry>> GetTimeEntriesAsync(int taskId);
    /// <summary>PL-8: cursor pagination alongside the unbounded list above.</summary>
    Task<(IEnumerable<TaskTimeEntry> Items, string? NextCursor)> GetTimeEntriesCursorAsync(int taskId, string? cursor, int limit);
    Task<bool> DeleteTimeEntryAsync(int timeEntryId, int employeeId);
    
    // Activity
    Task<TaskActivity> AddActivityAsync(int taskId, int actorId, string action, string? field, string? oldValue, string? newValue, string? reason, string? correlationId);
    Task<IEnumerable<TaskActivity>> GetActivitiesAsync(int taskId, int? page = null, int? limit = null);
    /// <summary>PL-8: cursor pagination alongside the page-based scheme above.</summary>
    Task<(IEnumerable<TaskActivity> Items, string? NextCursor)> GetActivitiesCursorAsync(int taskId, string? cursor, int limit);
    
    // Watchers
    Task<TaskWatcher> AddWatcherAsync(int taskId, int employeeId);
    Task<bool> RemoveWatcherAsync(int taskId, int employeeId);
    Task<IEnumerable<TaskWatcher>> GetWatchersAsync(int taskId);
    Task<bool> IsWatchingAsync(int taskId, int employeeId);
    
    // Tags
    Task<Tag> GetOrCreateTagAsync(string name);
    Task<Tag?> FindTagByNameAsync(string name);
    Task<bool> AddTagToTaskAsync(int taskId, int tagId);
    Task<bool> RemoveTagFromTaskAsync(int taskId, int tagId);
    Task<IEnumerable<Tag>> GetTagsForTaskAsync(int taskId);
    Task<IEnumerable<Tag>> GetAllTagsAsync();
    Task<bool> DeleteTagAsync(int tagId);
    Task<List<(Tag Tag, int Count)>> GetTagsWithUsageCountsAsync();

    // Department reporting (RP-1, RP-2) - DB-side aggregates, not fetch-all-then-count-in-C#.
    Task<(Dictionary<string, int> StatusCounts, int Overdue, int Unassigned)> GetDepartmentTaskSummaryAsync(int departmentId);
    Task<List<(int EmployeeId, string EmployeeName, Dictionary<string, int> PriorityCounts)>> GetDepartmentWorkloadAsync(int departmentId);
    
    // Notifications
    Task<Notification> CreateNotificationAsync(int recipientId, string type, string message, int? taskId = null);
    Task<IEnumerable<Notification>> GetNotificationsAsync(int recipientId, bool? unreadOnly = null, int? page = null, int? limit = null);
    /// <summary>PL-8: cursor pagination alongside the page-based scheme above.</summary>
    Task<(IEnumerable<Notification> Items, string? NextCursor)> GetNotificationsCursorAsync(int recipientId, bool? unreadOnly, string? cursor, int limit);
    Task<int> GetUnreadCountAsync(int recipientId);
    Task<bool> MarkNotificationReadAsync(int notificationId, int recipientId);
    Task<bool> MarkAllNotificationsReadAsync(int recipientId);

    // Comments
Task<TaskComment> AddCommentAsync(TaskComment comment);
Task<TaskComment?> GetCommentByIdAsync(int commentId);
Task<TaskComment> UpdateCommentAsync(TaskComment comment);
Task<bool> DeleteCommentAsync(int commentId);
Task<IEnumerable<TaskComment>> GetCommentsByTaskIdAsync(int taskId, int? page = null, int? limit = null);

// Checklist
Task<TaskChecklistItem?> GetChecklistItemByIdAsync(int itemId);

// ============================================
// Part B - Status Duration Tracking (WC-23..WC-27)
// ============================================
Task<TaskStatusInterval> OpenStatusIntervalAsync(int taskId, string status, int actorId, DateTime enteredAt);
Task CloseOpenStatusIntervalAsync(int taskId, DateTime exitedAt);
Task<IEnumerable<TaskStatusInterval>> GetStatusIntervalsAsync(int taskId);

// ============================================
// Part B - Acceptance Criteria / Definition of Done (WC-10..WC-14)
// ============================================
Task<TaskAcceptanceCriterion> AddAcceptanceCriterionAsync(int taskId, string text, int position);
Task<TaskAcceptanceCriterion?> GetAcceptanceCriterionByIdAsync(int id);
Task<TaskAcceptanceCriterion> UpdateAcceptanceCriterionAsync(int id, string? text, bool? isMet, int? metById);
Task<bool> DeleteAcceptanceCriterionAsync(int id);
Task<IEnumerable<TaskAcceptanceCriterion>> GetAcceptanceCriteriaAsync(int taskId);
Task CopyDepartmentDefaultCriteriaAsync(int taskId, int departmentId);

// Department default criteria (WC-12)
Task<DepartmentDefaultCriterion> AddDepartmentDefaultCriterionAsync(int departmentId, string text, int position);
Task<DepartmentDefaultCriterion?> GetDepartmentDefaultCriterionByIdAsync(int id);
Task<bool> DeleteDepartmentDefaultCriterionAsync(int id);
Task<IEnumerable<DepartmentDefaultCriterion>> GetDepartmentDefaultCriteriaAsync(int departmentId);
}
using StaffDesk.Core.Entities;
using StaffDesk.Core.Models;

namespace StaffDesk.Core.Interfaces;

public interface ITaskService
{
    // CRUD
    Task<WorkTask> CreateTaskAsync(
        string title,
        string? description,
        string departmentName,
        int createdById,
        int? assigneeId = null,
        string priority = "NORMAL",
        DateTime? dueAt = null,
        int? estimateMinutes = null,
        int? parentTaskId = null,
        List<string>? tags = null
    );
    
    Task<WorkTask?> GetTaskByIdAsync(int id, int viewerId);
    Task<WorkTask?> GetTaskByKeyAsync(string key, int viewerId);
    Task<WorkTask> UpdateTaskAsync(
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
        bool isArchived
    );
    Task<bool> DeleteTaskAsync(int taskId, int userId);
    Task<bool> RestoreTaskAsync(int taskId, int userId);
    
    // Query
    Task<(IEnumerable<WorkTask> Items, int TotalCount)> GetTasksAsync(
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
        bool? assigneeIsNull = null
    );
    
    Task<IEnumerable<WorkTask>> GetMyTasksAsync(int userId);
    
    // Status
    Task<WorkTask> TransitionStatusAsync(int taskId, int userId, string newStatus, string? reason = null, int? assigneeId = null, string? reworkCategory = null, string? outcome = null);
    Task<List<string>> GetAvailableTransitionsAsync(int taskId, int userId);
    Task<List<string>> GetAvailableTransitionsAsync(WorkTask task, int userId);

    // Assignment
    Task<WorkTask> AssignTaskAsync(int taskId, int userId, int? assigneeId);
    Task<bool> CanAssignAsync(int userId, int? targetEmployeeId, int departmentId, int? currentAssigneeId = null);
    Task<int> CountActiveTasksForEmployeeAsync(int employeeId);
    Task ReassignAllActiveTasksAsync(int fromEmployeeId, int toEmployeeId, int actorUserId);
    
    // Comments
    Task<TaskComment> AddCommentAsync(int taskId, int authorId, string body);
    Task<TaskComment> UpdateCommentAsync(int commentId, int userId, string body);
    Task<bool> DeleteCommentAsync(int commentId, int userId);
    Task<IEnumerable<TaskComment>> GetCommentsAsync(int taskId, int viewerId, int? page = null, int? limit = null);
    
    // Checklist
    Task<TaskChecklistItem> AddChecklistItemAsync(int taskId, int userId, string label, int position);
    Task<TaskChecklistItem> UpdateChecklistItemAsync(int itemId, int userId, string? label, bool? isDone, int? position);
    Task<bool> DeleteChecklistItemAsync(int itemId, int userId);
    Task<IEnumerable<TaskChecklistItem>> GetChecklistItemsAsync(int taskId, int userId);
    
    // Dependencies
    Task<TaskDependency> AddDependencyAsync(int blockedTaskId, int blockingTaskId, int userId);
    Task<bool> RemoveDependencyAsync(int blockedTaskId, int blockingTaskId, int userId);
    Task<IEnumerable<TaskDependency>> GetBlockedByAsync(int taskId, int userId);
    Task<IEnumerable<TaskDependency>> GetBlockingAsync(int taskId, int userId);
    
    // Time Entries
    Task<TaskTimeEntry> AddTimeEntryAsync(int taskId, int userId, int minutes, string? note, DateTime workedOn);
    Task<IEnumerable<TaskTimeEntry>> GetTimeEntriesAsync(int taskId, int userId);
    Task<(IEnumerable<TaskTimeEntry> Items, string? NextCursor)> GetTimeEntriesCursorAsync(int taskId, int userId, string? cursor, int limit);
    Task<bool> DeleteTimeEntryAsync(int timeEntryId, int userId);
    
    // Watchers
    Task<TaskWatcher> AddWatcherAsync(int taskId, int userId);
    Task<bool> RemoveWatcherAsync(int taskId, int userId);
    Task<IEnumerable<TaskWatcher>> GetWatchersAsync(int taskId, int userId);
    Task<bool> IsWatchingAsync(int taskId, int userId);
    // Watch/unwatch on behalf of self OR another employee (WT routes) - actorId is the caller,
    // targetEmployeeId is who ends up watching/unwatching.
    Task<TaskWatcher> AddWatcherForAsync(int taskId, int actorId, int targetEmployeeId);
    Task<bool> RemoveWatcherForAsync(int taskId, int actorId, int targetEmployeeId);

    // Tags
    Task<IEnumerable<Tag>> GetTagsAsync();
    Task<List<(Tag Tag, int Count)>> GetTagsWithUsageCountsAsync();
    Task<Tag> CreateTagAsync(string name, int userId);
    Task<bool> DeleteTagAsync(int tagId, int userId);
    Task<IEnumerable<Tag>> GetTagsForTaskAsync(int taskId, int userId);
    Task<IEnumerable<Tag>> ReplaceTaskTagsAsync(int taskId, int userId, List<string> tagNames);

    // Notifications
    Task<IEnumerable<Notification>> GetMyNotificationsAsync(int userId, bool? unreadOnly = null, int? page = null, int? limit = null);
    Task<(IEnumerable<Notification> Items, string? NextCursor)> GetMyNotificationsCursorAsync(int userId, bool? unreadOnly, string? cursor, int limit);
    Task<int> GetUnreadNotificationCountAsync(int userId);
    Task<bool> MarkNotificationReadAsync(int notificationId, int userId);
    Task<bool> MarkAllNotificationsReadAsync(int userId);
    
    // Validation & Permissions
    Task<bool> CanReadTaskAsync(int taskId, int userId);
    Task<bool> CanEditTaskAsync(int taskId, int userId);
    Task<bool> CanDeleteTaskAsync(int taskId, int userId);
    Task<bool> CanTransitionTaskAsync(int taskId, int userId, string newStatus);
    Task<Employee?> GetUserEmployeeAsync(int userId);

    // Activity
    Task<IEnumerable<TaskActivity>> GetTaskActivityAsync(int taskId, int userId, int? page = null, int? limit = null);
    Task<(IEnumerable<TaskActivity> Items, string? NextCursor)> GetTaskActivityCursorAsync(int taskId, int userId, string? cursor, int limit);

    // Archive
    Task<WorkTask> SetArchivedAsync(int taskId, int userId, bool isArchived);

    // Department reporting (RP-1, RP-2)
    Task<(Dictionary<string, int> StatusCounts, int Overdue, int Unassigned)> GetDepartmentTaskSummaryAsync(int departmentId);
    Task<List<(int EmployeeId, string EmployeeName, Dictionary<string, int> PriorityCounts)>> GetDepartmentWorkloadAsync(int departmentId);

    // ============================================
    // Part B - Status Duration Tracking (WC-23..WC-27)
    // ============================================
    Task<IEnumerable<TaskStatusInterval>> GetStatusIntervalsAsync(int taskId, int userId);

    // ============================================
    // Part B - Acceptance Criteria / Definition of Done (WC-10..WC-14)
    // ============================================
    Task<TaskAcceptanceCriterion> AddAcceptanceCriterionAsync(int taskId, int userId, string text);
    Task<TaskAcceptanceCriterion> UpdateAcceptanceCriterionAsync(int criterionId, int userId, string? text, bool? isMet);
    Task<bool> DeleteAcceptanceCriterionAsync(int criterionId, int userId);
    Task<IEnumerable<TaskAcceptanceCriterion>> GetAcceptanceCriteriaAsync(int taskId, int userId);
    Task<bool> CanManageAcceptanceCriteriaAsync(int taskId, int userId);

    // Department default criteria (WC-12)
    Task<DepartmentDefaultCriterion> AddDepartmentDefaultCriterionAsync(int departmentId, int userId, string text);
    Task<bool> DeleteDepartmentDefaultCriterionAsync(int criterionId, int userId);
    Task<IEnumerable<DepartmentDefaultCriterion>> GetDepartmentDefaultCriteriaAsync(int departmentId);

    // ============================================
// Bulk Operations (§5.8)
// ============================================
Task<BulkOperationResult> BulkUpdateStatusAsync(List<int> taskIds, string status, string? reason, int userId);
Task<BulkOperationResult> BulkUpdateAssigneeAsync(List<int> taskIds, int? assigneeId, int userId);
Task<BulkOperationResult> BulkDeleteAsync(List<int> taskIds, int userId);
Task<BulkOperationResult> BulkArchiveAsync(List<int> taskIds, bool isArchived, int userId);
Task<BulkOperationResult> BulkAddTagsAsync(List<int> taskIds, List<string> tags, int userId);
Task<BulkOperationResult> BulkRemoveTagsAsync(List<int> taskIds, List<string> tags, int userId);

}
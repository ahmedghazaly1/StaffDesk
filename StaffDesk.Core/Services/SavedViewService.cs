using System.Text.Json;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Exceptions;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;

namespace StaffDesk.Core.Services;

public class SavedViewService : ISavedViewService
{
    private static readonly string[] ValidVisibilities = { "PRIVATE", "DEPARTMENT", "ORGANISATION" };

    private readonly ISavedViewRepository _viewRepository;
    private readonly ITaskService _taskService;
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public SavedViewService(
        ISavedViewRepository viewRepository,
        ITaskService taskService,
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository)
    {
        _viewRepository = viewRepository;
        _taskService = taskService;
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<SavedView> CreateAsync(int ownerId, string name, string visibility, int? departmentId, string filterJson)
    {
        Validate(name, visibility, departmentId, filterJson);

        var view = new SavedView
        {
            Name = name.Trim(),
            OwnerId = ownerId,
            Visibility = visibility.ToUpperInvariant(),
            DepartmentId = visibility.Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase) ? departmentId : null,
            FilterJson = filterJson
        };

        return await _viewRepository.CreateAsync(view);
    }

    public async Task<SavedView?> GetByIdAsync(int id, int viewerId)
    {
        var view = await _viewRepository.GetByIdAsync(id);
        if (view == null) return null;
        if (!await CanViewAsync(view, viewerId)) return null;
        return view;
    }

    public async Task<IEnumerable<SavedView>> ListForUserAsync(int userId)
    {
        var user = await _userRepository.GetByEmployeeIdAsync(userId);
        var employee = await _employeeRepository.GetByIdAsync(userId);
        var isAdmin = user?.Role == "Admin";
        return await _viewRepository.GetVisibleForUserAsync(userId, employee?.DepartmentId, isAdmin);
    }

    public async Task<SavedView> UpdateAsync(int id, int userId, string name, string visibility, int? departmentId, string filterJson)
    {
        var view = await _viewRepository.GetByIdAsync(id);
        if (view == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Saved view not found", 404);
        if (view.OwnerId != userId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Only the owner may update this view", 403);

        Validate(name, visibility, departmentId, filterJson);

        view.Name = name.Trim();
        view.Visibility = visibility.ToUpperInvariant();
        view.DepartmentId = visibility.Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase) ? departmentId : null;
        view.FilterJson = filterJson;

        return await _viewRepository.UpdateAsync(view);
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var view = await _viewRepository.GetByIdAsync(id);
        if (view == null) return false;
        if (view.OwnerId != userId)
            throw new TaskDomainException(TaskErrorCodes.Forbidden, "Only the owner may delete this view", 403);
        return await _viewRepository.DeleteAsync(id);
    }

    public async Task<(IEnumerable<WorkTask> Items, int TotalCount)> ApplyViewAsync(int viewId, int viewerId, int? page, int? limit)
    {
        var view = await GetByIdAsync(viewId, viewerId);
        if (view == null)
            throw new TaskDomainException(TaskErrorCodes.NotFound, "Saved view not found", 404);

        var filter = JsonSerializer.Deserialize<SavedViewFilter>(view.FilterJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new SavedViewFilter();

        return await _taskService.GetTasksAsync(
            viewerId,
            page,
            limit,
            filter.Search,
            filter.Status,
            filter.Priority,
            filter.AssigneeId,
            filter.DepartmentId,
            filter.CreatedById,
            filter.WatchedByMe,
            filter.Tag,
            filter.DueBefore,
            filter.DueAfter,
            filter.Overdue,
            filter.IncludeArchived,
            filter.ParentTaskId,
            filter.Sort
        );
    }

    private static void Validate(string name, string visibility, int? departmentId, string filterJson)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "Name must be between 1 and 80 characters", 400);

        if (!ValidVisibilities.Contains(visibility.ToUpperInvariant()))
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "Visibility must be PRIVATE, DEPARTMENT, or ORGANISATION", 400);

        if (visibility.Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase) && !departmentId.HasValue)
            throw new TaskDomainException(TaskErrorCodes.ValidationError,
                "DepartmentId is required for DEPARTMENT visibility", 400);

        try
        {
            JsonSerializer.Deserialize<SavedViewFilter>(filterJson);
        }
        catch
        {
            throw new TaskDomainException(TaskErrorCodes.ValidationError, "FilterJson is not valid JSON", 400);
        }
    }

    private async Task<bool> CanViewAsync(SavedView view, int viewerId)
    {
        if (view.OwnerId == viewerId) return true;

        var user = await _userRepository.GetByEmployeeIdAsync(viewerId);
        if (user?.Role == "Admin") return true;

        if (view.Visibility == "ORGANISATION") return true;

        if (view.Visibility == "DEPARTMENT" && view.DepartmentId.HasValue)
        {
            var employee = await _employeeRepository.GetByIdAsync(viewerId);
            return employee?.DepartmentId == view.DepartmentId;
        }

        return false;
    }
}

using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ISavedViewService
{
    Task<SavedView> CreateAsync(int ownerId, string name, string visibility, int? departmentId, string filterJson);
    Task<SavedView?> GetByIdAsync(int id, int viewerId);
    Task<IEnumerable<SavedView>> ListForUserAsync(int userId);
    Task<SavedView> UpdateAsync(int id, int userId, string name, string visibility, int? departmentId, string filterJson);
    Task<bool> DeleteAsync(int id, int userId);
    Task<(IEnumerable<WorkTask> Items, int TotalCount)> ApplyViewAsync(int viewId, int viewerId, int? page, int? limit);
}

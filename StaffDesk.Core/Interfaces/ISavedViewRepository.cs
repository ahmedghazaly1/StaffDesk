using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ISavedViewRepository
{
    Task<SavedView> CreateAsync(SavedView view);
    Task<SavedView?> GetByIdAsync(int id);
    Task<IEnumerable<SavedView>> GetVisibleForUserAsync(int userId, int? departmentId, bool isAdmin);
    Task<SavedView> UpdateAsync(SavedView view);
    Task<bool> DeleteAsync(int id);
}

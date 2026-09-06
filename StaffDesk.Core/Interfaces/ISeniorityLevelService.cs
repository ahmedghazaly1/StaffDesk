using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ISeniorityLevelService
{
    Task<IEnumerable<SeniorityLevel>> GetAllAsync();
    Task<SeniorityLevel?> GetByIdAsync(int id);
    Task<SeniorityLevel> CreateAsync(string name, int rank, string? description);
    Task<SeniorityLevel> UpdateAsync(int id, string name, int rank, string? description, bool isActive);
    Task<bool> DeleteAsync(int id);
}
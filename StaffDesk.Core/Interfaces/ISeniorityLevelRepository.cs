using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface ISeniorityLevelRepository
{
    Task<IEnumerable<SeniorityLevel>> GetAllAsync();
    Task<SeniorityLevel?> GetByIdAsync(int id);
    Task<SeniorityLevel> CreateAsync(SeniorityLevel level);
    Task<SeniorityLevel> UpdateAsync(SeniorityLevel level);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> IsInUseAsync(int id);  // Check if any employee references this level
    Task<bool> HasCompetencyDescriptorsAsync(int id);  // CompetencyLevelDescriptors.SeniorityLevelId is also RESTRICT
}
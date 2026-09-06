using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;

namespace StaffDesk.Core.Services;

public class SeniorityLevelService : ISeniorityLevelService
{
    private readonly ISeniorityLevelRepository _repository;

    public SeniorityLevelService(ISeniorityLevelRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<SeniorityLevel>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<SeniorityLevel?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<SeniorityLevel> CreateAsync(string name, int rank, string? description)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Level name is required");
        
        if (name.Length > 40)
            throw new ArgumentException("Level name must be 40 characters or less");

        if (rank <= 0)
            throw new ArgumentException("Rank must be a positive number");

        if (description != null && description.Length > 200)
            throw new ArgumentException("Description must be 200 characters or less");

        var existing = await _repository.GetAllAsync();
        if (existing.Any(l => l.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A level with this name already exists");
        if (existing.Any(l => l.Rank == rank))
            throw new ArgumentException("A level with this rank already exists");

        var level = new SeniorityLevel
        {
            Name = name.Trim(),
            Rank = rank,
            Description = description?.Trim(),
            IsActive = true
        };

        return await _repository.CreateAsync(level);
    }

    public async Task<SeniorityLevel> UpdateAsync(int id, string name, int rank, string? description, bool isActive)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Level name is required");
        
        if (name.Length > 40)
            throw new ArgumentException("Level name must be 40 characters or less");

        if (rank <= 0)
            throw new ArgumentException("Rank must be a positive number");

        if (description != null && description.Length > 200)
            throw new ArgumentException("Description must be 200 characters or less");

        var level = await _repository.GetByIdAsync(id);
        if (level == null)
            throw new ArgumentException("Seniority level not found");

        var existing = await _repository.GetAllAsync();
        if (existing.Any(l => l.Id != id && l.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A level with this name already exists");
        if (existing.Any(l => l.Id != id && l.Rank == rank))
            throw new ArgumentException("A level with this rank already exists");

        level.Name = name.Trim();
        level.Rank = rank;
        level.Description = description?.Trim();
        level.IsActive = isActive;

        return await _repository.UpdateAsync(level);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // Check if level is in use by any employee
        var isInUse = await _repository.IsInUseAsync(id);
        if (isInUse)
            throw new InvalidOperationException("Cannot delete a seniority level that is currently assigned to employees. Deactivate it instead.");

        // CompetencyLevelDescriptors.SeniorityLevelId is also a RESTRICT foreign key - without
        // this check, deleting a level with descriptors hit the DB's own FK violation, which
        // (same pattern as the department-delete bug) surfaced as an unhandled 500 rather than a
        // clean 409, because it happened inside the same DbContext that later flushes the
        // audit-write's SaveChangesAsync.
        var hasDescriptors = await _repository.HasCompetencyDescriptorsAsync(id);
        if (hasDescriptors)
            throw new InvalidOperationException("Cannot delete a seniority level that has competency descriptors defined. Deactivate it instead.");

        return await _repository.DeleteAsync(id);
    }
}
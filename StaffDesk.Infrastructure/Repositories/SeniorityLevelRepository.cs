using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class SeniorityLevelRepository : ISeniorityLevelRepository
{
    private readonly AppDbContext _context;

    public SeniorityLevelRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SeniorityLevel>> GetAllAsync()
    {
        return await _context.SeniorityLevels
            .OrderBy(l => l.Rank)
            .ToListAsync();
    }

    public async Task<SeniorityLevel?> GetByIdAsync(int id)
    {
        return await _context.SeniorityLevels
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<SeniorityLevel> CreateAsync(SeniorityLevel level)
    {
        _context.SeniorityLevels.Add(level);
        await _context.SaveChangesAsync();
        return level;
    }

    public async Task<SeniorityLevel> UpdateAsync(SeniorityLevel level)
    {
        _context.SeniorityLevels.Update(level);
        await _context.SaveChangesAsync();
        return level;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var level = await _context.SeniorityLevels.FindAsync(id);
        if (level == null) return false;
        
        _context.SeniorityLevels.Remove(level);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.SeniorityLevels.AnyAsync(l => l.Id == id);
    }

    public async Task<bool> IsInUseAsync(int id)
    {
        return await _context.Employees.AnyAsync(e => e.LevelId == id);
    }

    public async Task<bool> HasCompetencyDescriptorsAsync(int id)
    {
        return await _context.CompetencyLevelDescriptors.AnyAsync(d => d.SeniorityLevelId == id);
    }
}
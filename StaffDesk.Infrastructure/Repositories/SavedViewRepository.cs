using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class SavedViewRepository : ISavedViewRepository
{
    private readonly AppDbContext _context;

    public SavedViewRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SavedView> CreateAsync(SavedView view)
    {
        view.CreatedAt = DateTime.UtcNow;
        view.UpdatedAt = DateTime.UtcNow;
        _context.SavedViews.Add(view);
        await _context.SaveChangesAsync();

        // Reload with Owner included so callers mapping straight to a response DTO get a
        // populated ownerName instead of an empty string.
        return await _context.SavedViews
            .Include(v => v.Owner)
            .FirstAsync(v => v.Id == view.Id);
    }

    public async Task<SavedView?> GetByIdAsync(int id)
    {
        return await _context.SavedViews
            .Include(v => v.Owner)
            .Include(v => v.Department)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<IEnumerable<SavedView>> GetVisibleForUserAsync(int userId, int? departmentId, bool isAdmin)
    {
        var query = _context.SavedViews
            .Include(v => v.Owner)
            .Include(v => v.Department)
            .AsQueryable();

        if (isAdmin)
            return await query.OrderBy(v => v.Name).ToListAsync();

        return await query
            .Where(v =>
                v.OwnerId == userId ||
                v.Visibility == "ORGANISATION" ||
                (v.Visibility == "DEPARTMENT" && v.DepartmentId == departmentId))
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<SavedView> UpdateAsync(SavedView view)
    {
        view.UpdatedAt = DateTime.UtcNow;
        _context.SavedViews.Update(view);
        await _context.SaveChangesAsync();
        return view;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var view = await _context.SavedViews.FindAsync(id);
        if (view == null) return false;
        _context.SavedViews.Remove(view);
        await _context.SaveChangesAsync();
        return true;
    }
}

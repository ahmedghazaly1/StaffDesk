using Microsoft.EntityFrameworkCore;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Infrastructure.Data;

namespace StaffDesk.Infrastructure.Repositories;

public class PerformanceRepository : IPerformanceRepository
{
    private readonly AppDbContext _db;
    public PerformanceRepository(AppDbContext db) => _db = db;

    public async Task<ReviewCycle> AddCycleAsync(ReviewCycle cycle)
    {
        _db.ReviewCycles.Add(cycle);
        await _db.SaveChangesAsync();
        return cycle;
    }

    public Task<ReviewCycle?> GetCycleAsync(int id, bool tracking = true)
    {
        var q = tracking ? _db.ReviewCycles.AsQueryable() : _db.ReviewCycles.AsNoTracking();
        return q.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task UpdateCycleAsync(ReviewCycle cycle)
    {
        _db.ReviewCycles.Update(cycle);
        await _db.SaveChangesAsync();
    }

    public async Task AddReviewsAsync(IEnumerable<PerformanceReview> reviews)
    {
        _db.PerformanceReviews.AddRange(reviews);
        await _db.SaveChangesAsync();
    }

    public Task<PerformanceReview?> GetReviewAsync(int id, bool tracking = true)
    {
        IQueryable<PerformanceReview> q = tracking ? _db.PerformanceReviews : _db.PerformanceReviews.AsNoTracking();
        return q
            .Include(r => r.ReviewCycle)
            .Include(r => r.Employee).ThenInclude(e => e.Level)
            .Include(r => r.Reviewer)
            .Include(r => r.Evidence)
            .Include(r => r.Appeals)
            .Include(r => r.PeerInvitations).ThenInclude(p => p.Feedback)
            .Include(r => r.PeerInvitations).ThenInclude(p => p.Nominee)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IReadOnlyList<PerformanceReview>> GetReviewsForCycleAsync(int cycleId) =>
        await _db.PerformanceReviews.AsNoTracking()
            .Include(r => r.Employee)
            .Include(r => r.Reviewer)
            .Where(r => r.ReviewCycleId == cycleId)
            .ToListAsync();

    public async Task<IReadOnlyList<PerformanceReview>> GetReviewsForEmployeeAsync(int employeeId) =>
        await _db.PerformanceReviews.AsNoTracking()
            .Include(r => r.ReviewCycle)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task UpdateReviewAsync(PerformanceReview review)
    {
        review.UpdatedAt = DateTime.UtcNow;
        _db.PerformanceReviews.Update(review);
        await _db.SaveChangesAsync();
    }

    public async Task<ReviewEvidence> AddEvidenceAsync(ReviewEvidence evidence)
    {
        _db.ReviewEvidences.Add(evidence);
        await _db.SaveChangesAsync();
        return evidence;
    }

    public async Task<ReviewAppeal> AddAppealAsync(ReviewAppeal appeal)
    {
        _db.ReviewAppeals.Add(appeal);
        await _db.SaveChangesAsync();
        return appeal;
    }

    public async Task<PeerInvitation> AddPeerInvitationAsync(PeerInvitation invitation)
    {
        _db.PeerInvitations.Add(invitation);
        await _db.SaveChangesAsync();
        return invitation;
    }

    public async Task UpdatePeerInvitationAsync(PeerInvitation invitation)
    {
        _db.PeerInvitations.Update(invitation);
        await _db.SaveChangesAsync();
    }

    public Task<PeerInvitation?> GetPeerInvitationByTokenAsync(string token, bool tracking = true)
    {
        IQueryable<PeerInvitation> q = tracking ? _db.PeerInvitations : _db.PeerInvitations.AsNoTracking();
        return q
            .Include(p => p.PerformanceReview).ThenInclude(r => r.ReviewCycle)
            .Include(p => p.Feedback)
            .FirstOrDefaultAsync(p => p.Token == token);
    }

    public async Task<PeerFeedback> AddPeerFeedbackAsync(PeerFeedback feedback)
    {
        _db.PeerFeedbacks.Add(feedback);
        await _db.SaveChangesAsync();
        return feedback;
    }

    public Task<int> CountSubmittedPeersAsync(int reviewId) =>
        _db.PeerInvitations.CountAsync(p =>
            p.PerformanceReviewId == reviewId && p.Status == PeerInvitationStatuses.Submitted);

    public async Task<Goal> AddGoalAsync(Goal goal)
    {
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();
        return goal;
    }

    public Task<Goal?> GetGoalAsync(int id, bool tracking = true)
    {
        IQueryable<Goal> q = tracking ? _db.Goals : _db.Goals.AsNoTracking();
        return q
            .Include(g => g.Owner)
            .Include(g => g.Manager)
            .Include(g => g.Versions)
            .Include(g => g.TaskLinks)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task UpdateGoalAsync(Goal goal)
    {
        goal.UpdatedAt = DateTime.UtcNow;
        _db.Goals.Update(goal);
        await _db.SaveChangesAsync();
    }

    public async Task AddGoalVersionAsync(GoalVersion version)
    {
        _db.GoalVersions.Add(version);
        await _db.SaveChangesAsync();
    }

    public async Task<FeedbackNote> AddFeedbackNoteAsync(FeedbackNote note)
    {
        _db.FeedbackNotes.Add(note);
        await _db.SaveChangesAsync();
        return note;
    }

    public Task<FeedbackNote?> GetFeedbackNoteAsync(int id) =>
        _db.FeedbackNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);

    public async Task<IReadOnlyList<Competency>> GetCompetenciesAsync() =>
        await _db.Competencies.AsNoTracking()
            .Include(c => c.LevelDescriptors).ThenInclude(d => d.SeniorityLevel)
            .Where(c => c.IsActive)
            .OrderBy(c => c.Category).ThenBy(c => c.Name)
            .ToListAsync();

    public Task<Employee?> GetEmployeeAsync(int id) =>
        _db.Employees.Include(e => e.Level).FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IReadOnlyList<Employee>> GetActiveEmployeesInDepartmentsAsync(IEnumerable<int> departmentIds)
    {
        var ids = departmentIds.ToList();
        return await _db.Employees.AsNoTracking()
            .Include(e => e.Level)
            .Where(e => e.IsActive && !e.IsErased && ids.Contains(e.DepartmentId))
            .ToListAsync();
    }

    public async Task<HashSet<int>> GetReportIdsAsync(int managerEmployeeId, bool includeIndirect)
    {
        var all = await _db.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.Id, e.ManagerId })
            .ToListAsync();

        var result = new HashSet<int>();
        var frontier = all.Where(e => e.ManagerId == managerEmployeeId).Select(e => e.Id).ToList();
        foreach (var id in frontier) result.Add(id);

        if (includeIndirect)
        {
            var queue = new Queue<int>(frontier);
            while (queue.Count > 0)
            {
                var m = queue.Dequeue();
                foreach (var child in all.Where(e => e.ManagerId == m))
                {
                    if (result.Add(child.Id))
                        queue.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    public Task<bool> TaskExistsAsync(int taskId) =>
        _db.Tasks.AnyAsync(t => t.Id == taskId && t.DeletedAt == null);

    public async Task SeedCompetenciesIfEmptyAsync(IEnumerable<Competency> competencies)
    {
        if (await _db.Competencies.AnyAsync()) return;
        _db.Competencies.AddRange(competencies);
        await _db.SaveChangesAsync();
    }

    public Task<bool> HasReviewedInCycleAsync(int reviewerEmployeeId, int cycleId) =>
        _db.PerformanceReviews.AsNoTracking()
            .AnyAsync(r => r.ReviewCycleId == cycleId && r.ReviewerEmployeeId == reviewerEmployeeId);

    public async Task<Dictionary<int, string>> GetTaskStatusesAsync(IEnumerable<int> taskIds)
    {
        var ids = taskIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();
        return await _db.Tasks.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Status);
    }

    public async Task<(int SampleSize, int TotalRework, int TotalReopen)> GetQualityIndicatorsAsync(
        int employeeId, DateOnly periodStart, DateOnly periodEnd)
    {
        var start = DateTime.SpecifyKind(periodStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(periodEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var rows = await _db.Tasks.AsNoTracking()
            .Where(t => t.AssigneeId == employeeId
                        && t.DeletedAt == null
                        && t.Status == "DONE"
                        && t.CompletedAt != null
                        && t.CompletedAt >= start && t.CompletedAt <= end)
            .Select(t => new { t.ReworkCount, t.ReopenCount })
            .ToListAsync();

        return (rows.Count, rows.Sum(r => r.ReworkCount), rows.Sum(r => r.ReopenCount));
    }
}

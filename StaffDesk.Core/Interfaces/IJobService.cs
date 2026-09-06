using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IJobService
{
    Task<Job> EnqueueAsync(string type, object payload);
    Task<Job?> GetJobAsync(long id);
    Task ProcessNextJobAsync();
    Task<IReadOnlyList<Job>> ListJobsAsync(string? state, int limit = 50);
    Task<(IReadOnlyList<Job> Items, int Total)> ListJobsPagedAsync(string? state, int page, int limit);
    Task<bool> RequeueJobAsync(long id);
}

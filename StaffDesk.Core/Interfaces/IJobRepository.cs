using StaffDesk.Core.Entities;

namespace StaffDesk.Core.Interfaces;

public interface IJobRepository
{
    Task<Job> EnqueueAsync(string type, string payloadJson);
    Task<Job?> ClaimNextAsync();
    Task MarkSucceededAsync(long jobId);
    Task MarkFailedAsync(long jobId, string error, int maxAttempts = 5);
    Task<Job?> GetByIdAsync(long id);
    Task<IReadOnlyDictionary<string, int>> CountByStateAsync();
    Task<double?> GetOldestQueuedAgeSecondsAsync();
    Task<IReadOnlyList<Job>> ListByStateAsync(string? state, int limit = 50);
    Task<(IReadOnlyList<Job> Items, int Total)> ListPagedAsync(string? state, int page, int limit);
    Task<bool> RequeueAsync(long jobId);
}

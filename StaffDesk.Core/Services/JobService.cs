using System.Text.Json;
using StaffDesk.Core.Constants;
using StaffDesk.Core.Entities;
using StaffDesk.Core.Interfaces;
using StaffDesk.Core.Models;

namespace StaffDesk.Core.Services;

public class JobService : IJobService
{
    private readonly IJobRepository _jobRepository;
    private readonly ITaskService _taskService;
    private readonly ISlaService _slaService;
    private readonly IAuditService _auditService;
    private readonly IGovernanceService _governanceService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IWebhookDispatchService _webhooks;

    public JobService(
        IJobRepository jobRepository,
        ITaskService taskService,
        ISlaService slaService,
        IAuditService auditService,
        IGovernanceService governanceService,
        IAnalyticsService analyticsService,
        IWebhookDispatchService webhooks)
    {
        _jobRepository = jobRepository;
        _taskService = taskService;
        _slaService = slaService;
        _auditService = auditService;
        _governanceService = governanceService;
        _analyticsService = analyticsService;
        _webhooks = webhooks;
    }

    public async Task<Job> EnqueueAsync(string type, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return await _jobRepository.EnqueueAsync(type, json);
    }

    public Task<Job?> GetJobAsync(long id) => _jobRepository.GetByIdAsync(id);

    public Task<IReadOnlyList<Job>> ListJobsAsync(string? state, int limit = 50) =>
        _jobRepository.ListByStateAsync(state, limit);

    public Task<(IReadOnlyList<Job> Items, int Total)> ListJobsPagedAsync(string? state, int page, int limit) =>
        _jobRepository.ListPagedAsync(state, page, limit);

    public Task<bool> RequeueJobAsync(long id) => _jobRepository.RequeueAsync(id);

    public async Task ProcessNextJobAsync()
    {
        var job = await _jobRepository.ClaimNextAsync();
        if (job == null) return;

        try
        {
            switch (job.Type)
            {
                case JobTypes.SlaEvaluate:
                    await _slaService.EvaluateAllTasksAsync();
                    break;
                case JobTypes.BulkStatus:
                    await RunBulkStatusAsync(job.PayloadJson);
                    break;
                case JobTypes.BulkAssignee:
                    await RunBulkAssigneeAsync(job.PayloadJson);
                    break;
                case JobTypes.BulkArchive:
                    await RunBulkArchiveAsync(job.PayloadJson);
                    break;
                case JobTypes.BulkDelete:
                    await RunBulkDeleteAsync(job.PayloadJson);
                    break;
                case JobTypes.BulkAddTags:
                    await RunBulkTagsAsync(job.PayloadJson, add: true);
                    break;
                case JobTypes.BulkRemoveTags:
                    await RunBulkTagsAsync(job.PayloadJson, add: false);
                    break;
                case JobTypes.AuditExport:
                {
                    var p = JsonSerializer.Deserialize<GovernanceJobPayload>(job.PayloadJson)!;
                    await _governanceService.ProcessAuditExportAsync(p.ExportId!.Value);
                    break;
                }
                case JobTypes.SubjectAccessExport:
                {
                    var p = JsonSerializer.Deserialize<GovernanceJobPayload>(job.PayloadJson)!;
                    await _governanceService.ProcessSubjectAccessExportAsync(p.ExportId!.Value);
                    break;
                }
                case JobTypes.Purge:
                {
                    var p = JsonSerializer.Deserialize<GovernanceJobPayload>(job.PayloadJson)!;
                    await _governanceService.ProcessPurgeAsync(p.PurgeRunId!.Value);
                    break;
                }
                case JobTypes.Erasure:
                {
                    var p = JsonSerializer.Deserialize<GovernanceJobPayload>(job.PayloadJson)!;
                    await _governanceService.ProcessErasureAsync(p.SubjectEmployeeId!.Value, p.ActorEmployeeId!.Value);
                    break;
                }
                case JobTypes.MetricRollup:
                {
                    var p = JsonSerializer.Deserialize<AnalyticsRollupPayload>(job.PayloadJson) ?? new AnalyticsRollupPayload();
                    var date = DateOnly.TryParse(p.Date, out var d)
                        ? d
                        : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
                    if (p.DepartmentId.HasValue)
                    {
                        await _analyticsService.RollupDepartmentDayAsync(p.DepartmentId.Value, date);
                    }
                    else
                    {
                        await _analyticsService.RebuildAsync(date, date);
                    }
                    break;
                }
                case JobTypes.MetricRebuild:
                {
                    var p = JsonSerializer.Deserialize<AnalyticsRebuildPayload>(job.PayloadJson)!;
                    var from = DateOnly.Parse(p.From!);
                    var to = DateOnly.Parse(p.To!);
                    await _analyticsService.RebuildAsync(from, to, p.DepartmentId);
                    break;
                }
                case JobTypes.AnalyticsExport:
                {
                    var p = JsonSerializer.Deserialize<AnalyticsExportPayload>(job.PayloadJson)!;
                    await _analyticsService.ProcessAnalyticsExportAsync(p.ExportId);
                    break;
                }
                case JobTypes.WebhookDispatch:
                {
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var p = JsonSerializer.Deserialize<WebhookDispatchPayload>(job.PayloadJson, opts)
                            ?? throw new InvalidOperationException("Invalid WEBHOOK_DISPATCH payload");
                    await _webhooks.DispatchAsync(p.SubscriptionId, p.EventType, p.Body, p.ReplayOfDeliveryId);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unknown job type: {job.Type}");
            }

            await _jobRepository.MarkSucceededAsync(job.Id);
        }
        catch (Exception ex)
        {
            await _jobRepository.MarkFailedAsync(job.Id, ex.Message);
        }
    }

    private async Task RunBulkStatusAsync(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<BulkJobPayload>(payloadJson)!;
        var result = await _taskService.BulkUpdateStatusAsync(payload.TaskIds, payload.Status!, payload.Reason, payload.UserId);
        await LogBulkAuditAsync(payload.UserId, JobTypes.BulkStatus, result);
    }

    private async Task RunBulkAssigneeAsync(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<BulkJobPayload>(payloadJson)!;
        var result = await _taskService.BulkUpdateAssigneeAsync(payload.TaskIds, payload.AssigneeId, payload.UserId);
        await LogBulkAuditAsync(payload.UserId, JobTypes.BulkAssignee, result);
    }

    private async Task RunBulkArchiveAsync(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<BulkJobPayload>(payloadJson)!;
        var result = await _taskService.BulkArchiveAsync(payload.TaskIds, payload.IsArchived ?? false, payload.UserId);
        await LogBulkAuditAsync(payload.UserId, JobTypes.BulkArchive, result);
    }

    private async Task RunBulkDeleteAsync(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<BulkJobPayload>(payloadJson)!;
        var result = await _taskService.BulkDeleteAsync(payload.TaskIds, payload.UserId);
        await LogBulkAuditAsync(payload.UserId, JobTypes.BulkDelete, result);
    }

    private async Task RunBulkTagsAsync(string payloadJson, bool add)
    {
        var payload = JsonSerializer.Deserialize<BulkJobPayload>(payloadJson)!;
        var result = add
            ? await _taskService.BulkAddTagsAsync(payload.TaskIds, payload.Tags ?? new List<string>(), payload.UserId)
            : await _taskService.BulkRemoveTagsAsync(payload.TaskIds, payload.Tags ?? new List<string>(), payload.UserId);
        await LogBulkAuditAsync(payload.UserId, add ? JobTypes.BulkAddTags : JobTypes.BulkRemoveTags, result);
    }

    private async Task LogBulkAuditAsync(int userId, string operation, BulkOperationResult result)
    {
        await _auditService.LogAsync(
            "BULK_OPERATION_EXECUTED",
            userId,
            userId.ToString(),
            "SUCCESS",
            "BulkOperation",
            operation,
            null,
            new { operation, result.TotalProcessed, result.TotalSuccessful, result.TotalFailed },
            null, null, null);
    }
}

public class BulkJobPayload
{
    public List<int> TaskIds { get; set; } = new();
    public int UserId { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
    public int? AssigneeId { get; set; }
    public bool? IsArchived { get; set; }
    public List<string>? Tags { get; set; }
}

public class GovernanceJobPayload
{
    public long? ExportId { get; set; }
    public long? PurgeRunId { get; set; }
    public int? SubjectEmployeeId { get; set; }
    public int? ActorEmployeeId { get; set; }
}

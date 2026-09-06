using StaffDesk.Core.Interfaces;

namespace StaffDesk.Worker.Jobs;

public class JobWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobWorker> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SlaInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MetricRollupInterval = TimeSpan.FromHours(24);
    private DateTime _lastSlaEnqueue = DateTime.MinValue;
    private DateTime _lastMetricEnqueue = DateTime.MinValue;

    public JobWorker(IServiceProvider serviceProvider, ILogger<JobWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StaffDesk job worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}",
                ["WorkerInstance"] = Environment.MachineName
            }))
            {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                var operations = scope.ServiceProvider.GetRequiredService<IOperationsService>();

                await operations.TouchWorkerHeartbeatAsync(Environment.MachineName, stoppingToken);

                if (DateTime.UtcNow - _lastSlaEnqueue >= SlaInterval)
                {
                    await jobRepository.EnqueueAsync(
                        StaffDesk.Core.Constants.JobTypes.SlaEvaluate,
                        "{}");
                    _lastSlaEnqueue = DateTime.UtcNow;
                }

                // AN-3: nightly rollup for yesterday (UTC)
                if (DateTime.UtcNow - _lastMetricEnqueue >= MetricRollupInterval)
                {
                    var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
                    await jobRepository.EnqueueAsync(
                        StaffDesk.Core.Constants.JobTypes.MetricRollup,
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            Date = yesterday.ToString("yyyy-MM-dd")
                        }));
                    _lastMetricEnqueue = DateTime.UtcNow;
                }

                await jobService.ProcessNextJobAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job worker iteration failed");
            }
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}

namespace ConflictDuplicationDetector.Api.Services;

public class BackgroundJobProcessor : BackgroundService
{
    private readonly JobService _jobService;
    private readonly ILogger<BackgroundJobProcessor> _logger;

    public BackgroundJobProcessor(IJobService jobService, ILogger<BackgroundJobProcessor> logger)
    {
        _jobService = (JobService)jobService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background job processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _jobService.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing job queue");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("Background job processor stopped");
    }
}

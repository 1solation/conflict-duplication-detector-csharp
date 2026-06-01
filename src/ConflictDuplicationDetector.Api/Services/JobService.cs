using System.Collections.Concurrent;
using System.Threading.Channels;
using ConflictDuplicationDetector.Api.Models;

namespace ConflictDuplicationDetector.Api.Services;

public class JobService : IJobService
{
    private readonly ConcurrentDictionary<Guid, JobEntry> _jobs = new();
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly ILogger<JobService> _logger;

    public JobService(ILogger<JobService> logger)
    {
        _logger = logger;
    }

    public async Task<JobAcceptedResponse> EnqueueAsync(
        JobType type,
        Func<CancellationToken, Task<object>> work,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid();
        var entry = new JobEntry
        {
            Id = jobId,
            Type = type,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Work = work
        };

        _jobs[jobId] = entry;
        await _queue.Writer.WriteAsync(jobId, cancellationToken);

        return new JobAcceptedResponse(
            jobId,
            type,
            JobStatus.Pending,
            $"/api/jobs/{jobId}");
    }

    public JobStatusResponse? GetJob(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
            return null;

        return ToResponse(entry);
    }

    public IReadOnlyList<JobStatusResponse> ListJobs(int limit = 50)
    {
        return _jobs.Values
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .Select(ToResponse)
            .ToList();
    }

    internal async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        var jobId = await _queue.Reader.ReadAsync(cancellationToken);
        if (!_jobs.TryGetValue(jobId, out var entry))
            return;

        entry.Status = JobStatus.Running;
        entry.StartedAt = DateTime.UtcNow;

        try
        {
            entry.Result = await entry.Work(cancellationToken);
            entry.Status = JobStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} ({Type}) failed", jobId, entry.Type);
            entry.Status = JobStatus.Failed;
            entry.Error = ex.Message;
        }
        finally
        {
            entry.CompletedAt = DateTime.UtcNow;
        }
    }

    private static JobStatusResponse ToResponse(JobEntry entry) =>
        new(
            entry.Id,
            entry.Type,
            entry.Status,
            entry.CreatedAt,
            entry.StartedAt,
            entry.CompletedAt,
            entry.Error,
            entry.Status == JobStatus.Completed ? entry.Result : null);

    internal sealed class JobEntry
    {
        public Guid Id { get; init; }
        public JobType Type { get; init; }
        public JobStatus Status { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Error { get; set; }
        public object? Result { get; set; }
        public Func<CancellationToken, Task<object>> Work { get; init; } = _ => Task.FromResult<object>(new());
    }
}

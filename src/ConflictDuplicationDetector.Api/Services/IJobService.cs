using ConflictDuplicationDetector.Api.Models;

namespace ConflictDuplicationDetector.Api.Services;

public interface IJobService
{
    Task<JobAcceptedResponse> EnqueueAsync(JobType type, Func<CancellationToken, Task<object>> work, CancellationToken cancellationToken = default);

    JobStatusResponse? GetJob(Guid jobId);

    IReadOnlyList<JobStatusResponse> ListJobs(int limit = 50);
}

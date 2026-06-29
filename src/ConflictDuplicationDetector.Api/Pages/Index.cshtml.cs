using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Api.Pages;

public class IndexModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    public IReadOnlyList<JobStatusResponse> RecentJobs { get; private set; } = [];

    public bool OpenAiConfigured => IsOpenAiConfigured;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        RecentJobs = jobs.ListJobs(5);
        await Task.CompletedTask;
    }
}

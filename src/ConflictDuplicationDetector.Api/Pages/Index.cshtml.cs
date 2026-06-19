using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ConflictDuplicationDetector.Api.Pages;

public class IndexModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    public KnowledgeBaseStatusResponse? KnowledgeBase { get; private set; }

    public IReadOnlyList<JobStatusResponse> RecentJobs { get; private set; } = [];

    public bool OpenAiConfigured => IsOpenAiConfigured;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (IsOpenAiConfigured)
        {
            var detector = Services.GetRequiredService<DetectorApplicationService>();
            KnowledgeBase = await detector.GetKnowledgeBaseStatusAsync(cancellationToken);
        }
        else
        {
            KnowledgeBase = new KnowledgeBaseStatusResponse(
                ProviderFormatter.Format(AppConfig.OpenAI),
                System.IO.File.Exists(AppConfig.VectorStore.PersistPath),
                0,
                AppConfig.VectorStore.PersistPath);
        }

        RecentJobs = jobs.ListJobs(5);
    }
}

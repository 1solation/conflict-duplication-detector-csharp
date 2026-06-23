using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;

namespace ConflictDuplicationDetector.Api.Pages;

public class KnowledgeBaseModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    public KnowledgeBaseDetailsResponse? KnowledgeBase { get; private set; }

    public KnowledgeBaseDashboardSummary? Dashboard { get; private set; }

    public LastIngestActivitySummary? LastIngest { get; private set; }

    public LastAnalysisActivitySummary? LastAnalysis { get; private set; }

    public bool OpenAiConfigured => IsOpenAiConfigured;

    public string ReadinessTagText => Dashboard?.Readiness switch
    {
        KnowledgeBaseReadiness.NotConfigured => "Setup required",
        KnowledgeBaseReadiness.Empty => "No documents uploaded",
        KnowledgeBaseReadiness.Ready => "Ready to analyse",
        _ => "Unknown"
    };

    public string ReadinessTagClass => Dashboard?.Readiness switch
    {
        KnowledgeBaseReadiness.NotConfigured => "govuk-tag govuk-tag--red",
        KnowledgeBaseReadiness.Empty => "govuk-tag govuk-tag--grey",
        KnowledgeBaseReadiness.Ready => "govuk-tag govuk-tag--green",
        _ => "govuk-tag"
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetDetector(out var detector))
        {
            KnowledgeBase = new KnowledgeBaseDetailsResponse(
                ProviderFormatter.Format(AppConfig.OpenAI),
                System.IO.File.Exists(AppConfig.VectorStore.PersistPath),
                0,
                0,
                AppConfig.VectorStore.PersistPath,
                [],
                []);

            Dashboard = new KnowledgeBaseDashboardSummary(
                KnowledgeBaseReadiness.NotConfigured,
                KnowledgeBase.Exists,
                0,
                0,
                0,
                string.Empty,
                []);
        }
        else
        {
            KnowledgeBase = await detector.GetKnowledgeBaseDetailsAsync(cancellationToken);
            Dashboard = DetectorApplicationService.BuildDashboardSummary(KnowledgeBase);
        }

        var allJobs = jobs.ListJobs(50);

        LastIngest = allJobs
            .Where(job => job.Type == JobType.Ingest
                && job.Status == JobStatus.Completed
                && job.CompletedAt.HasValue)
            .OrderByDescending(job => job.CompletedAt)
            .Select(job => new LastIngestActivitySummary(
                job.JobId,
                job.CompletedAt!.Value,
                job.Result is IngestionResult ingestion ? ingestion.DocumentsProcessed : 0))
            .FirstOrDefault();

        LastAnalysis = allJobs
            .Where(job => job.Type == JobType.Analysis
                && job.Status == JobStatus.Completed
                && job.CompletedAt.HasValue)
            .OrderByDescending(job => job.CompletedAt)
            .Select(job => job.Result is AnalysisResult analysis
                ? new LastAnalysisActivitySummary(
                    job.JobId,
                    job.CompletedAt!.Value,
                    analysis.Duplications.Count,
                    analysis.Conflicts.Count,
                    analysis.Inconsistencies.Count)
                : new LastAnalysisActivitySummary(job.JobId, job.CompletedAt!.Value, 0, 0, 0))
            .FirstOrDefault();
    }
}

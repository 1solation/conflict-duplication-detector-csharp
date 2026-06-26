using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConflictDuplicationDetector.Api.Pages;

public class KnowledgeBaseModel(AppConfiguration config, IServiceProvider services, IJobService jobs)
    : DetectorPageModel(config, services)
{
    private const string SortLatest = "latest";
    private const string SortEarliest = "earliest";

    public KnowledgeBaseDetailsResponse? KnowledgeBase { get; private set; }

    public KnowledgeBaseDashboardSummary? Dashboard { get; private set; }

    public LastIngestActivitySummary? LastIngest { get; private set; }

    public LastAnalysisActivitySummary? LastAnalysis { get; private set; }

    public IReadOnlyList<KnowledgeBaseDocumentRow> DocumentRows { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? DocumentName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortOrder { get; set; } = SortLatest;

    public bool OpenAiConfigured => IsOpenAiConfigured;

    public bool HasDocumentFilters => !string.IsNullOrWhiteSpace(DocumentName)
        || !string.Equals(NormalizedSortOrder, SortLatest, StringComparison.OrdinalIgnoreCase);

    public string NormalizedSortOrder => string.Equals(SortOrder, SortEarliest, StringComparison.OrdinalIgnoreCase)
        ? SortEarliest
        : SortLatest;

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

        SortOrder = NormalizedSortOrder;
        DocumentRows = BuildDocumentRows(KnowledgeBase?.Documents ?? []);
    }

    private IReadOnlyList<KnowledgeBaseDocumentRow> BuildDocumentRows(
        IReadOnlyList<KnowledgeBaseDocumentResponse> documents)
    {
        var rows = documents
            .Select(document => new KnowledgeBaseDocumentRow(
                document.DocumentId,
                document.SourceFile,
                GetDisplayDocumentName(document.SourceFile),
                document.ChunkCount,
                GetUploadedAt(document.SourceFile)));

        if (!string.IsNullOrWhiteSpace(DocumentName))
        {
            rows = rows.Where(row => row.DocumentName.Contains(
                DocumentName.Trim(),
                StringComparison.OrdinalIgnoreCase));
        }

        rows = NormalizedSortOrder == SortEarliest
            ? rows
                .OrderBy(row => row.UploadedAt is null)
                .ThenBy(row => row.UploadedAt)
                .ThenBy(row => row.DocumentName)
            : rows
                .OrderBy(row => row.UploadedAt is null)
                .ThenByDescending(row => row.UploadedAt)
                .ThenBy(row => row.DocumentName);

        return rows.ToList();
    }

    private static DateTime? GetUploadedAt(string sourceFile)
    {
        if (!System.IO.File.Exists(sourceFile))
            return null;

        return System.IO.File.GetLastWriteTimeUtc(sourceFile);
    }

    private static string GetDisplayDocumentName(string sourceFile)
    {
        var fileName = Path.GetFileName(sourceFile);
        var displayName = fileName;

        if (fileName.Length > 37
            && fileName[36] == '_'
            && Guid.TryParse(fileName[..36], out _))
        {
            displayName = fileName[37..];
        }

        return displayName.Replace("_", " ");
    }
}

public record KnowledgeBaseDocumentRow(
    string DocumentId,
    string SourceFile,
    string DocumentName,
    int ChunkCount,
    DateTime? UploadedAt);

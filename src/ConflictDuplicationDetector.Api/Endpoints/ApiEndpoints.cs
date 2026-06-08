using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ConflictDuplicationDetector.Api.Endpoints;

public static class ApiEndpoints
{
    public static void MapDetectorApi(this WebApplication app)
    {
        var api = app.MapGroup("/api").WithTags("Detector");

        api.MapGet("/health", GetHealth)
            .WithName("GetHealth")
            .WithSummary("Health check")
            .WithDescription("Returns API health and knowledge-base summary.")
            .Produces<HealthResponse>();

        api.MapGet("/knowledge-base", GetKnowledgeBase)
            .WithName("GetKnowledgeBase")
            .WithSummary("Knowledge base status")
            .WithDescription("Returns whether the vector store exists and how many chunks are loaded.")
            .Produces<KnowledgeBaseStatusResponse>();

        api.MapPost("/documents", IngestDocuments)
            .WithName("IngestDocuments")
            .WithSummary("Ingest documents")
            .WithDescription("""
                Upload one or more documents and enqueue ingestion into the knowledge base. Returns 202 with a job ID.
                
                **Supported file formats:**
                - PDF (.pdf) - `application/pdf`
                - Word (.docx) - `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
                - HTML (.html, .htm) - `text/html`
                - Plain text (.txt) - `text/plain`
                """)
            .Produces<JobAcceptedResponse>(StatusCodes.Status202Accepted)
            .DisableAntiforgery();

        api.MapPost("/analysis", StartAnalysis)
            .WithName("StartAnalysis")
            .WithSummary("Analyse the knowledge base")
            .WithDescription("Enqueue analysis of all ingested documents (duplications, conflicts, inconsistencies). Returns 202 with a job ID. Type can be 'all', 'duplications', 'conflicts', or 'inconsistencies'.")
            .Accepts<AnalysisJobRequest>("application/json")
            .Produces<JobAcceptedResponse>(StatusCodes.Status202Accepted);

        api.MapPost("/check", CheckDocument)
            .WithName("CheckDocument")
            .WithSummary("Check a file against the knowledge base")
            .WithDescription("""
                Upload a file and compare it against the ingested knowledge base without adding it. Returns 202 with a job ID.
                
                **Supported file formats:**
                - PDF (.pdf) - `application/pdf`
                - Word (.docx) - `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
                - HTML (.html, .htm) - `text/html`
                - Plain text (.txt) - `text/plain`
                
                **Query parameters:**
                - `type` (optional): 'all', 'duplications', 'conflicts', or 'inconsistencies' (default: 'all')
                """)
            .Produces<JobAcceptedResponse>(StatusCodes.Status202Accepted)
            .DisableAntiforgery();

        api.MapPost("/chat", StartChat)
            .WithName("StartChat")
            .WithSummary("Chat with the knowledge base")
            .WithDescription("Enqueue a natural-language query against ingested documents. Returns 202 with a job ID. The system auto-routes queries: use words like 'duplicate/copy' for duplication analysis, 'conflict/contradict' for conflict analysis, 'terminology/format' for inconsistency analysis.")
            .Accepts<ChatJobRequest>("application/json")
            .Produces<JobAcceptedResponse>(StatusCodes.Status202Accepted);

        api.MapGet("/jobs", ListJobs)
            .WithName("ListJobs")
            .WithSummary("List recent jobs")
            .WithDescription("Returns a list of recent jobs ordered by creation time. Optional query param 'limit' controls the number of jobs returned (default 50).")
            .Produces<List<JobStatusResponse>>();

        api.MapGet("/jobs/{jobId:guid}", GetJob)
            .WithName("GetJob")
            .WithSummary("Get job status and result")
            .WithDescription("Poll this endpoint until status is 'Completed' or 'Failed'. The 'result' field contains the analysis output when complete.")
            .Produces<JobStatusResponse>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> GetHealth(
        AppConfiguration config,
        DetectorApplicationService detector,
        CancellationToken cancellationToken)
    {
        var kb = await detector.GetKnowledgeBaseStatusAsync(cancellationToken);

        return Results.Ok(new HealthResponse(
            "healthy",
            !string.IsNullOrEmpty(config.OpenAI.ApiKey),
            kb.Exists,
            kb.ChunkCount));
    }

    private static async Task<IResult> GetKnowledgeBase(
        DetectorApplicationService detector,
        CancellationToken cancellationToken)
    {
        var status = await detector.GetKnowledgeBaseStatusAsync(cancellationToken);
        return Results.Ok(status);
    }

    private static async Task<IResult> IngestDocuments(
        IFormFileCollection files,
        DetectorApplicationService detector,
        IJobService jobs,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0 || files.All(f => f.Length == 0))
            return Results.BadRequest(new ProblemDetails { Title = "No files uploaded" });

        try
        {
            detector.EnsureApiKeyConfigured();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var savedPaths = await detector.SaveUploadsAsync(files, cancellationToken);
        var accepted = await jobs.EnqueueAsync(
            JobType.Ingest,
            async ct => await detector.IngestFilesAsync(savedPaths, ct),
            cancellationToken);

        return Results.Accepted(accepted.StatusUrl, accepted);
    }

    private static async Task<IResult> StartAnalysis(
        [FromBody] AnalysisJobRequest request,
        DetectorApplicationService detector,
        IJobService jobs,
        CancellationToken cancellationToken)
    {
        try
        {
            detector.EnsureApiKeyConfigured();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var accepted = await jobs.EnqueueAsync(
            JobType.Analysis,
            async ct => await detector.RunAnalysisAsync(request.Type, request.Topic, ct),
            cancellationToken);

        return Results.Accepted(accepted.StatusUrl, accepted);
    }

    private static async Task<IResult> CheckDocument(
        IFormFile file,
        string? type,
        DetectorApplicationService detector,
        IJobService jobs,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return Results.BadRequest(new ProblemDetails { Title = "No file uploaded" });

        try
        {
            detector.EnsureApiKeyConfigured();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var savedPath = await detector.SaveUploadAsync(file, cancellationToken);
        var analysisType = string.IsNullOrWhiteSpace(type) ? "all" : type;

        var accepted = await jobs.EnqueueAsync(
            JobType.Check,
            async ct => await detector.CheckFileAsync(savedPath, analysisType, ct),
            cancellationToken);

        return Results.Accepted(accepted.StatusUrl, accepted);
    }

    private static async Task<IResult> StartChat(
        [FromBody] ChatJobRequest request,
        DetectorApplicationService detector,
        IJobService jobs,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new ProblemDetails { Title = "Message is required" });

        try
        {
            detector.EnsureApiKeyConfigured();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var accepted = await jobs.EnqueueAsync(
            JobType.Chat,
            async ct => await detector.ChatAsync(request.Message, ct),
            cancellationToken);

        return Results.Accepted(accepted.StatusUrl, accepted);
    }

    private static IResult GetJob(Guid jobId, IJobService jobs)
    {
        var job = jobs.GetJob(jobId);
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static IResult ListJobs(IJobService jobs, int? limit)
    {
        return Results.Ok(jobs.ListJobs(limit ?? 50));
    }
}

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
            .WithDescription("Returns API health and knowledge-base summary.");

        api.MapGet("/knowledge-base", GetKnowledgeBase)
            .WithName("GetKnowledgeBase")
            .WithSummary("Knowledge base status")
            .WithDescription("Returns whether the vector store exists and how many chunks are loaded.");

        api.MapPost("/documents", IngestDocument)
            .WithName("IngestDocument")
            .WithSummary("Ingest a document")
            .WithDescription("Upload a document and enqueue ingestion into the knowledge base. Returns 202 with a job ID.")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery();

        api.MapPost("/analysis", StartAnalysis)
            .WithName("StartAnalysis")
            .WithSummary("Analyze the knowledge base")
            .WithDescription("Enqueue analysis of all ingested documents (duplications, conflicts, inconsistencies). Returns 202 with a job ID.");

        api.MapPost("/check", CheckDocument)
            .WithName("CheckDocument")
            .WithSummary("Check a file against the knowledge base")
            .WithDescription("Upload a file and compare it against the ingested knowledge base. Returns 202 with a job ID.")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery();

        api.MapPost("/chat", StartChat)
            .WithName("StartChat")
            .WithSummary("Chat with the knowledge base")
            .WithDescription("Enqueue a natural-language query against ingested documents. Returns 202 with a job ID.");

        api.MapGet("/jobs", ListJobs)
            .WithName("ListJobs")
            .WithSummary("List recent jobs");

        api.MapGet("/jobs/{jobId:guid}", GetJob)
            .WithName("GetJob")
            .WithSummary("Get job status and result")
            .WithDescription("Poll this endpoint until status is completed or failed.");
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

    private static async Task<IResult> IngestDocument(
        IFormFile file,
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
        var accepted = await jobs.EnqueueAsync(
            JobType.Ingest,
            async ct => await detector.IngestFileAsync(savedPath, ct),
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

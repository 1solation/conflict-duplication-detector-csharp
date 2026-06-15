using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Api.Models;

public static class ProviderFormatter
{
    public static string Format(OpenAIConfiguration config) =>
        config.UseAzure ? "Azure OpenAI" : "OpenAI";
}

/// <summary>Status of a background job.</summary>
public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

/// <summary>Type of background operation.</summary>
public enum JobType
{
    Ingest,
    Analysis,
    Check,
    Chat
}

/// <summary>Summary returned when a job is accepted.</summary>
public record JobAcceptedResponse(string Provider, Guid JobId, JobType Type, JobStatus Status, string StatusUrl);

/// <summary>Full job state including result when complete.</summary>
public record JobStatusResponse(
    string Provider,
    Guid JobId,
    JobType Type,
    JobStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Error,
    object? Result);

/// <summary>Request body for knowledge-base analysis.</summary>
public record AnalysisJobRequest(string Type = "all", string? Topic = null);

/// <summary>Request body for chat against the knowledge base.</summary>
public record ChatJobRequest(string Message);

/// <summary>Knowledge base status.</summary>
public record KnowledgeBaseStatusResponse(string Provider, bool Exists, int ChunkCount, string PersistPath);

/// <summary>API health check response.</summary>
public record HealthResponse(string Provider, string Status, bool OpenAiConfigured, bool KnowledgeBaseExists, int ChunkCount);

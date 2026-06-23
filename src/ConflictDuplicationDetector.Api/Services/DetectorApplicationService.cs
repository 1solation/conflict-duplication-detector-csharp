using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Core.Agents;
using ConflictDuplicationDetector.Core.Documents;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Api.Services;

public class DetectorApplicationService
{
    private readonly AppConfiguration _config;
    private readonly IVectorStoreCoordinator _coordinator;
    private readonly IDocumentIngestionService _ingestionService;
    private readonly IAnalysisService _analysisService;
    private readonly IFileAnalysisService _fileAnalysisService;
    private readonly IDocumentParserFactory _parserFactory;
    private readonly IDocumentChunker _chunker;
    private readonly IChatClient _chatClient;
    private readonly MetricsTracker _metricsTracker;
    private readonly AnalysisConfiguration _analysisConfig;

    public DetectorApplicationService(
        AppConfiguration config,
        IVectorStoreCoordinator coordinator,
        IDocumentIngestionService ingestionService,
        IAnalysisService analysisService,
        IFileAnalysisService fileAnalysisService,
        IDocumentParserFactory parserFactory,
        IDocumentChunker chunker,
        IChatClient chatClient,
        MetricsTracker metricsTracker,
        AnalysisConfiguration analysisConfig)
    {
        _config = config;
        _coordinator = coordinator;
        _ingestionService = ingestionService;
        _analysisService = analysisService;
        _fileAnalysisService = fileAnalysisService;
        _parserFactory = parserFactory;
        _chunker = chunker;
        _chatClient = chatClient;
        _metricsTracker = metricsTracker;
        _analysisConfig = analysisConfig;
    }

    public void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrEmpty(_config.OpenAI.ApiKey))
            throw new InvalidOperationException("OpenAI API key not configured. Set OPENAI_API_KEY.");
    }

    public async Task<string> SaveUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_config.Storage.UploadsPath);
        var safeName = Path.GetFileName(file.FileName);
        var path = Path.Combine(_config.Storage.UploadsPath, $"{Guid.NewGuid()}_{safeName}");

        await using var stream = File.Create(path);
        await file.CopyToAsync(stream, cancellationToken);
        return path;
    }

    public async Task<IReadOnlyList<string>> SaveUploadsAsync(IFormFileCollection files, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_config.Storage.UploadsPath);
        var savedPaths = new List<string>();

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var safeName = Path.GetFileName(file.FileName);
            var path = Path.Combine(_config.Storage.UploadsPath, $"{Guid.NewGuid()}_{safeName}");

            await using var stream = File.Create(path);
            await file.CopyToAsync(stream, cancellationToken);
            savedPaths.Add(path);
        }

        return savedPaths;
    }

    public async Task<IngestionResult> IngestFileAsync(string filePath, CancellationToken cancellationToken)
    {
        return await _coordinator.ExecuteExclusiveAsync(
            async (_, ct) => await _ingestionService.IngestFileAsync(filePath, ct),
            saveAfter: true,
            cancellationToken);
    }

    public async Task<IngestionResult> IngestFilesAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken)
    {
        return await _coordinator.ExecuteExclusiveAsync(
            async (_, ct) => await _ingestionService.IngestFilesAsync(filePaths, ct),
            saveAfter: true,
            cancellationToken);
    }

    public async Task<AnalysisResult> RunAnalysisAsync(string type, string? topic, CancellationToken cancellationToken)
    {
        await _coordinator.EnsureLoadedAsync(cancellationToken);
        var chunkCount = await _coordinator.GetChunkCountAsync(cancellationToken);
        if (chunkCount == 0)
            throw new InvalidOperationException("Knowledge base is empty. Ingest documents first.");

        var analysisType = type.ToLowerInvariant();
        return analysisType switch
        {
            "duplications" or "duplication" => await _analysisService.RunDuplicationAnalysisAsync(cancellationToken),
            "conflicts" or "conflict" => await _analysisService.RunConflictAnalysisAsync(topic, cancellationToken),
            "inconsistencies" or "inconsistency" => await _analysisService.RunInconsistencyAnalysisAsync(topic, cancellationToken),
            "all" => await _analysisService.RunFullAnalysisAsync(cancellationToken),
            _ => throw new ArgumentException($"Unknown analysis type: {type}")
        };
    }

    public async Task<AnalysisResult> CheckFileAsync(string filePath, string type, CancellationToken cancellationToken)
    {
        var parser = _parserFactory.GetParser(filePath)
            ?? throw new ArgumentException($"Unsupported file type: {Path.GetExtension(filePath)}");

        await _coordinator.EnsureLoadedAsync(cancellationToken);
        var chunkCount = await _coordinator.GetChunkCountAsync(cancellationToken);
        if (chunkCount == 0)
            throw new InvalidOperationException("Knowledge base is empty. Ingest documents first.");

        var document = await parser.ParseAsync(filePath, cancellationToken);
        var fileChunks = _chunker.ChunkDocument(document, _analysisConfig.ChunkSize, _analysisConfig.ChunkOverlap).ToList();

        return await _fileAnalysisService.AnalyseFileAsync(fileChunks, document, type, cancellationToken);
    }

    public async Task<Core.Agents.ChatResponse> ChatAsync(string message, CancellationToken cancellationToken)
    {
        await _coordinator.EnsureLoadedAsync(cancellationToken);
        return await _analysisService.ChatAsync(message, cancellationToken);
    }

    public async Task<KnowledgeBaseStatusResponse> GetKnowledgeBaseStatusAsync(CancellationToken cancellationToken)
    {
        var exists = await _coordinator.KnowledgeBaseExistsAsync(cancellationToken);
        var count = exists ? await _coordinator.GetChunkCountAsync(cancellationToken) : 0;
        return new KnowledgeBaseStatusResponse(
            ProviderFormatter.Format(_config.OpenAI),
            exists,
            count,
            _config.VectorStore.PersistPath);
    }

    public async Task<KnowledgeBaseDashboardSummary> GetKnowledgeBaseDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_config.OpenAI.ApiKey))
        {
            return new KnowledgeBaseDashboardSummary(
                KnowledgeBaseReadiness.NotConfigured,
                File.Exists(_config.VectorStore.PersistPath),
                0,
                0,
                0,
                string.Empty,
                []);
        }

        var details = await GetKnowledgeBaseDetailsAsync(cancellationToken);
        return BuildDashboardSummary(details);
    }

    public static KnowledgeBaseDashboardSummary BuildDashboardSummary(KnowledgeBaseDetailsResponse details)
    {
        var readiness = !details.Exists || details.ChunkCount == 0
            ? KnowledgeBaseReadiness.Empty
            : KnowledgeBaseReadiness.Ready;

        var recentDocuments = details.Documents.Take(5).ToList();
        var averageChunks = details.DocumentCount > 0
            ? (double)details.ChunkCount / details.DocumentCount
            : 0;

        var fileTypeBreakdown = string.Join(
            " · ",
            details.Documents
                .GroupBy(document => Path.GetExtension(document.SourceFile).ToLowerInvariant())
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Count()} {FormatFileType(group.Key)}"));

        return new KnowledgeBaseDashboardSummary(
            readiness,
            details.Exists,
            details.DocumentCount,
            details.ChunkCount,
            averageChunks,
            fileTypeBreakdown,
            recentDocuments);
    }

    public async Task<KnowledgeBaseDetailsResponse> GetKnowledgeBaseDetailsAsync(CancellationToken cancellationToken)
    {
        var exists = await _coordinator.KnowledgeBaseExistsAsync(cancellationToken);
        if (!exists)
        {
            return new KnowledgeBaseDetailsResponse(
                ProviderFormatter.Format(_config.OpenAI),
                false,
                0,
                0,
                _config.VectorStore.PersistPath,
                [],
                []);
        }

        await _coordinator.EnsureLoadedAsync(cancellationToken);
        var chunks = await _coordinator.VectorStore.GetAllChunksAsync(cancellationToken);
        var chunkResponses = chunks
            .OrderBy(chunk => chunk.SourceFile)
            .ThenBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new KnowledgeBaseChunkResponse(
                chunk.ChunkId,
                chunk.DocumentId,
                chunk.SourceFile,
                chunk.ChunkIndex,
                chunk.PageNumber,
                chunk.Section,
                ToExcerpt(chunk.Content)))
            .ToList();

        var documents = chunkResponses
            .GroupBy(chunk => new { chunk.DocumentId, chunk.SourceFile })
            .OrderBy(group => group.Key.SourceFile)
            .Select(group => new KnowledgeBaseDocumentResponse(
                group.Key.DocumentId,
                group.Key.SourceFile,
                group.Count()))
            .ToList();

        return new KnowledgeBaseDetailsResponse(
            ProviderFormatter.Format(_config.OpenAI),
            true,
            chunkResponses.Count,
            documents.Count,
            _config.VectorStore.PersistPath,
            documents,
            chunkResponses);
    }

    private static string ToExcerpt(string content)
    {
        var normalized = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 240 ? normalized : $"{normalized[..240]}...";
    }

    private static string FormatFileType(string extension) => extension switch
    {
        ".pdf" => "PDF",
        ".docx" => "DOCX",
        ".html" or ".htm" => "HTML",
        ".txt" => "TXT",
        _ => string.IsNullOrEmpty(extension) ? "Other" : extension.TrimStart('.').ToUpperInvariant()
    };
}

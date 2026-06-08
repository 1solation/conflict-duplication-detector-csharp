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

        return await _fileAnalysisService.AnalyzeFileAsync(fileChunks, document, type, cancellationToken);
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
        return new KnowledgeBaseStatusResponse(exists, count, _config.VectorStore.PersistPath);
    }
}

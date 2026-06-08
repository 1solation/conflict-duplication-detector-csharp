using ConflictDuplicationDetector.Core.Documents;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;

namespace ConflictDuplicationDetector.Core.Services;

public interface IDocumentIngestionService
{
    Task<IngestionResult> IngestFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<IngestionResult> IngestFilesAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default);
    Task<IngestionResult> IngestDirectoryAsync(string directoryPath, bool recursive = true, CancellationToken cancellationToken = default);
    Task<int> GetDocumentCountAsync(CancellationToken cancellationToken = default);
}

public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IDocumentParserFactory _parserFactory;
    private readonly IDocumentChunker _chunker;
    private readonly IVectorStore _vectorStore;
    private readonly AnalysisConfiguration _config;
    
    public DocumentIngestionService(
        IDocumentParserFactory parserFactory,
        IDocumentChunker chunker,
        IVectorStore vectorStore,
        AnalysisConfiguration config)
    {
        _parserFactory = parserFactory;
        _chunker = chunker;
        _vectorStore = vectorStore;
        _config = config;
    }
    
    public async Task<IngestionResult> IngestFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = new IngestionResult();
        
        try
        {
            var parser = _parserFactory.GetParser(filePath);
            if (parser == null)
            {
                result.Errors.Add($"No parser available for file: {filePath}");
                return result;
            }
            
            var document = await parser.ParseAsync(filePath, cancellationToken);
            result.DocumentsProcessed = 1;
            
            var chunks = _chunker.ChunkDocument(document, _config.ChunkSize, _config.ChunkOverlap).ToList();
            
            var newChunks = 0;
            var skippedChunks = 0;
            
            foreach (var chunk in chunks)
            {
                if (await _vectorStore.ChunkExistsAsync(chunk.ContentHash, cancellationToken))
                {
                    skippedChunks++;
                    continue;
                }
                
                await _vectorStore.AddChunkAsync(chunk, cancellationToken);
                newChunks++;
            }
            
            result.ChunksCreated = newChunks;
            result.ChunksSkipped = skippedChunks;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error processing {filePath}: {ex.Message}");
        }
        
        return result;
    }

    public async Task<IngestionResult> IngestFilesAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default)
    {
        var result = new IngestionResult();

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileResult = await IngestFileAsync(filePath, cancellationToken);

            result.DocumentsProcessed += fileResult.DocumentsProcessed;
            result.ChunksCreated += fileResult.ChunksCreated;
            result.ChunksSkipped += fileResult.ChunksSkipped;
            result.Errors.AddRange(fileResult.Errors);
        }

        result.Success = result.Errors.Count == 0;
        return result;
    }
    
    public async Task<IngestionResult> IngestDirectoryAsync(string directoryPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        var result = new IngestionResult();
        
        if (!Directory.Exists(directoryPath))
        {
            result.Errors.Add($"Directory not found: {directoryPath}");
            return result;
        }
        
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var supportedExtensions = _parserFactory.GetSupportedExtensions().ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();
        
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileResult = await IngestFileAsync(file, cancellationToken);
            
            result.DocumentsProcessed += fileResult.DocumentsProcessed;
            result.ChunksCreated += fileResult.ChunksCreated;
            result.ChunksSkipped += fileResult.ChunksSkipped;
            result.Errors.AddRange(fileResult.Errors);
        }
        
        result.Success = result.Errors.Count == 0;
        return result;
    }
    
    public async Task<int> GetDocumentCountAsync(CancellationToken cancellationToken = default)
    {
        return await _vectorStore.GetChunkCountAsync(cancellationToken);
    }
}

public class IngestionResult
{
    public bool Success { get; set; }
    public int DocumentsProcessed { get; set; }
    public int ChunksCreated { get; set; }
    public int ChunksSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
    
    public int TotalChunks => ChunksCreated + ChunksSkipped;
}

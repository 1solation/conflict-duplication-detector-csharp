using System.Text.Json;
using Build5Nines.SharpVector;
using Build5Nines.SharpVector.OpenAI;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using OpenAI;
using OpenAI.Embeddings;

namespace ConflictDuplicationDetector.Core.VectorStore;

public class SharpVectorStore : IVectorStore
{
    private readonly OpenAIMemoryVectorDatabase<ChunkMetadata> _vectorDb;
    private readonly Dictionary<string, ChunkMetadata> _metadataStore = new();
    private readonly HashSet<string> _contentHashes = new();
    private readonly object _lock = new();

    public SharpVectorStore(OpenAIConfiguration config)
    {
        var factory = new AIClientFactory();
        var embeddingClient = factory.CreateEmbeddingClient(config);
        _vectorDb = new OpenAIMemoryVectorDatabase<ChunkMetadata>(embeddingClient);
    }
    
    public SharpVectorStore(EmbeddingClient embeddingClient)
    {
        _vectorDb = new OpenAIMemoryVectorDatabase<ChunkMetadata>(embeddingClient);
    }
    
    [Obsolete("Use constructor with OpenAIConfiguration instead for full provider support")]
    public SharpVectorStore(string openAiApiKey, string embeddingModel = "text-embedding-3-small")
    {
        var openAIClient = new OpenAIClient(openAiApiKey);
        var embeddingClient = openAIClient.GetEmbeddingClient(embeddingModel);
        _vectorDb = new OpenAIMemoryVectorDatabase<ChunkMetadata>(embeddingClient);
    }
    
    public async Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        var metadata = new ChunkMetadata
        {
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            SourceFile = chunk.SourceFile,
            ChunkIndex = chunk.ChunkIndex,
            PageNumber = chunk.PageNumber,
            Section = chunk.Section,
            ContentHash = chunk.ContentHash,
            Content = chunk.Content
        };
        
        await _vectorDb.AddTextAsync(chunk.Content, metadata);
        
        lock (_lock)
        {
            _metadataStore[chunk.Id] = metadata;
            _contentHashes.Add(chunk.ContentHash);
        }
    }
    
    public async Task AddChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AddChunkAsync(chunk, cancellationToken);
        }
    }
    
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string query, int topK = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty. Use GetAllChunksAsync to retrieve all chunks.", nameof(query));
        }
        
        var searchResults = await _vectorDb.SearchAsync(query, pageCount: topK);
        
        return searchResults.Texts
            .Select(r => new VectorSearchResult
            {
                ChunkId = r.Metadata?.ChunkId ?? string.Empty,
                DocumentId = r.Metadata?.DocumentId ?? string.Empty,
                Content = r.Text,
                SimilarityScore = r.Similarity,
                SourceFile = r.Metadata?.SourceFile ?? string.Empty,
                ChunkIndex = r.Metadata?.ChunkIndex ?? 0,
                PageNumber = r.Metadata?.PageNumber,
                Section = r.Metadata?.Section,
                ContentHash = r.Metadata?.ContentHash ?? string.Empty
            })
            .ToList();
    }
    
    public async Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(string chunkId, int topK = 10, CancellationToken cancellationToken = default)
    {
        ChunkMetadata? sourceMetadata;
        lock (_lock)
        {
            if (!_metadataStore.TryGetValue(chunkId, out sourceMetadata))
                return Array.Empty<VectorSearchResult>();
        }
        
        if (string.IsNullOrWhiteSpace(sourceMetadata.Content))
            return Array.Empty<VectorSearchResult>();
        
        var allResults = await _vectorDb.SearchAsync(sourceMetadata.Content, pageCount: topK + 1);
        
        return allResults.Texts
            .Where(r => r.Metadata?.ChunkId != chunkId)
            .Take(topK)
            .Select(r => new VectorSearchResult
            {
                ChunkId = r.Metadata?.ChunkId ?? string.Empty,
                DocumentId = r.Metadata?.DocumentId ?? string.Empty,
                Content = r.Text,
                SimilarityScore = r.Similarity,
                SourceFile = r.Metadata?.SourceFile ?? string.Empty,
                ChunkIndex = r.Metadata?.ChunkIndex ?? 0,
                PageNumber = r.Metadata?.PageNumber,
                Section = r.Metadata?.Section,
                ContentHash = r.Metadata?.ContentHash ?? string.Empty
            })
            .ToList();
    }
    
    public Task<bool> ChunkExistsAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_contentHashes.Contains(contentHash));
        }
    }
    
    public Task<int> GetChunkCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_metadataStore.Count);
        }
    }
    
    public Task<IReadOnlyList<VectorSearchResult>> GetAllChunksAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var results = _metadataStore.Values
                .Select(m => new VectorSearchResult
                {
                    ChunkId = m.ChunkId,
                    DocumentId = m.DocumentId,
                    Content = m.Content,
                    SimilarityScore = 1.0,
                    SourceFile = m.SourceFile,
                    ChunkIndex = m.ChunkIndex,
                    PageNumber = m.PageNumber,
                    Section = m.Section,
                    ContentHash = m.ContentHash
                })
                .ToList();
            
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
        }
    }
    
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _metadataStore.Clear();
            _contentHashes.Clear();
        }
        return Task.CompletedTask;
    }
    
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        var state = new VectorStoreState
        {
            Metadata = _metadataStore.Values.ToList(),
            ContentHashes = _contentHashes.ToList()
        };
        
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }
    
    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return;
            
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var state = JsonSerializer.Deserialize<VectorStoreState>(json);
        
        if (state == null)
            return;
        
        lock (_lock)
        {
            _metadataStore.Clear();
            _contentHashes.Clear();
        }
        
        foreach (var metadata in state.Metadata)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (string.IsNullOrWhiteSpace(metadata.Content))
                continue;
            
            await _vectorDb.AddTextAsync(metadata.Content, metadata);
            
            lock (_lock)
            {
                _metadataStore[metadata.ChunkId] = metadata;
                if (!string.IsNullOrEmpty(metadata.ContentHash))
                    _contentHashes.Add(metadata.ContentHash);
            }
        }
    }
}

public class ChunkMetadata
{
    public string ChunkId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string? PageNumber { get; set; }
    public string? Section { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

internal class VectorStoreState
{
    public List<ChunkMetadata> Metadata { get; set; } = new();
    public List<string> ContentHashes { get; set; } = new();
}

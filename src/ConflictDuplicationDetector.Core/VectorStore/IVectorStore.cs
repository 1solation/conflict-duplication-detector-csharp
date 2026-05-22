using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Core.VectorStore;

public interface IVectorStore
{
    Task AddChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
    Task AddChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string query, int topK = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchSimilarAsync(string chunkId, int topK = 10, CancellationToken cancellationToken = default);
    Task<bool> ChunkExistsAsync(string contentHash, CancellationToken cancellationToken = default);
    Task<int> GetChunkCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorSearchResult>> GetAllChunksAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string path, CancellationToken cancellationToken = default);
    Task LoadAsync(string path, CancellationToken cancellationToken = default);
}

public class VectorSearchResult
{
    public string ChunkId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string? PageNumber { get; set; }
    public string? Section { get; set; }
    public string ContentHash { get; set; } = string.Empty;
}

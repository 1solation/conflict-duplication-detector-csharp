using ConflictDuplicationDetector.Core.VectorStore;

namespace ConflictDuplicationDetector.Core.Services;

public interface IVectorStoreCoordinator
{
    IVectorStore VectorStore { get; }

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task<int> GetChunkCountAsync(CancellationToken cancellationToken = default);

    Task<bool> KnowledgeBaseExistsAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteExclusiveAsync<T>(
        Func<IVectorStore, CancellationToken, Task<T>> operation,
        bool saveAfter = false,
        CancellationToken cancellationToken = default);
}

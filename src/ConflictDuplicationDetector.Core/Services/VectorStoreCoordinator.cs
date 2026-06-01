using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;

namespace ConflictDuplicationDetector.Core.Services;

public class VectorStoreCoordinator : IVectorStoreCoordinator
{
    private readonly IVectorStore _vectorStore;
    private readonly string _persistPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _loaded;

    public VectorStoreCoordinator(IVectorStore vectorStore, AppConfiguration configuration)
    {
        _vectorStore = vectorStore;
        _persistPath = configuration.VectorStore.PersistPath;
    }

    public IVectorStore VectorStore => _vectorStore;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await LoadIfNeededAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await PersistAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> GetChunkCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return await _vectorStore.GetChunkCountAsync(cancellationToken);
    }

    public Task<bool> KnowledgeBaseExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(_persistPath));
    }

    public async Task<T> ExecuteExclusiveAsync<T>(
        Func<IVectorStore, CancellationToken, Task<T>> operation,
        bool saveAfter = false,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await LoadIfNeededAsync(cancellationToken);
            var result = await operation(_vectorStore, cancellationToken);
            if (saveAfter)
                await PersistAsync(cancellationToken);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task LoadIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
            return;

        if (File.Exists(_persistPath))
            await _vectorStore.LoadAsync(_persistPath, cancellationToken);

        _loaded = true;
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_persistPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await _vectorStore.SaveAsync(_persistPath, cancellationToken);
        _loaded = true;
    }
}

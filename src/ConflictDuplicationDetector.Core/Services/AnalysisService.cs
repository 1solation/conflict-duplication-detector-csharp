using System.Text.Json;
using ConflictDuplicationDetector.Core.Agents;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Core.Services;

public interface IAnalysisService
{
    Task<AnalysisResult> RunFullAnalysisAsync(CancellationToken cancellationToken = default);
    Task<AnalysisResult> RunDuplicationAnalysisAsync(CancellationToken cancellationToken = default);
    Task<AnalysisResult> RunConflictAnalysisAsync(string? topic = null, CancellationToken cancellationToken = default);
    Task<AnalysisResult> RunInconsistencyAnalysisAsync(string? focusArea = null, CancellationToken cancellationToken = default);
    Task<Agents.ChatResponse> ChatAsync(string message, CancellationToken cancellationToken = default);
    Task SaveResultsAsync(AnalysisResult result, string outputPath, CancellationToken cancellationToken = default);
}

public class AnalysisService : IAnalysisService
{
    private readonly OrchestratorAgent _orchestrator;
    private readonly IVectorStore _vectorStore;
    private readonly string _persistPath;
    
    public AnalysisService(
        IChatClient chatClient,
        IVectorStore vectorStore,
        MetricsTracker metricsTracker,
        AnalysisConfiguration analysisConfig,
        string persistPath)
    {
        _vectorStore = vectorStore;
        _persistPath = persistPath;
        _orchestrator = new OrchestratorAgent(
            chatClient, 
            vectorStore, 
            metricsTracker, 
            analysisConfig.DuplicationThreshold);
    }
    
    public async Task<AnalysisResult> RunFullAnalysisAsync(CancellationToken cancellationToken = default)
    {
        await LoadVectorStoreAsync(cancellationToken);
        var result = await _orchestrator.RunFullAnalysisAsync(cancellationToken);
        await SaveVectorStoreAsync(cancellationToken);
        return result;
    }
    
    public async Task<AnalysisResult> RunDuplicationAnalysisAsync(CancellationToken cancellationToken = default)
    {
        await LoadVectorStoreAsync(cancellationToken);
        return await _orchestrator.RunDuplicationAnalysisAsync(cancellationToken);
    }
    
    public async Task<AnalysisResult> RunConflictAnalysisAsync(string? topic = null, CancellationToken cancellationToken = default)
    {
        await LoadVectorStoreAsync(cancellationToken);
        return await _orchestrator.RunConflictAnalysisAsync(topic, cancellationToken);
    }
    
    public async Task<AnalysisResult> RunInconsistencyAnalysisAsync(string? focusArea = null, CancellationToken cancellationToken = default)
    {
        await LoadVectorStoreAsync(cancellationToken);
        return await _orchestrator.RunInconsistencyAnalysisAsync(focusArea, cancellationToken);
    }
    
    public async Task<Agents.ChatResponse> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        await LoadVectorStoreAsync(cancellationToken);
        return await _orchestrator.ChatAsync(message, cancellationToken);
    }
    
    public async Task SaveResultsAsync(AnalysisResult result, string outputPath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }
    
    private async Task LoadVectorStoreAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_persistPath))
        {
            await _vectorStore.LoadAsync(_persistPath, cancellationToken);
        }
    }
    
    private async Task SaveVectorStoreAsync(CancellationToken cancellationToken)
    {
        await _vectorStore.SaveAsync(_persistPath, cancellationToken);
    }
}

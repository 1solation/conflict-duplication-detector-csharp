using System.Text.Json;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Core.Agents;

public class DuplicationAgent : BaseAgent
{
    private readonly double _similarityThreshold;
    
    public DuplicationAgent(
        IChatClient chatClient, 
        IVectorStore vectorStore, 
        MetricsTracker metricsTracker,
        double similarityThreshold = 0.85) 
        : base(chatClient, vectorStore, metricsTracker, "DuplicationAgent")
    {
        _similarityThreshold = similarityThreshold;
    }
    
    private static readonly Lazy<string> _systemPrompt = new(() => LoadPromptFromFile("DuplicationAgent.txt"));
    protected override string SystemPrompt => _systemPrompt.Value;
    
    public async Task<List<DuplicationResult>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DuplicationResult>();
        var processedPairs = new HashSet<string>();
        
        var allChunks = await VectorStore.GetAllChunksAsync(cancellationToken);
        
        foreach (var chunk in allChunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.Content))
                continue;
                
            var similarChunks = await VectorStore.SearchAsync(chunk.Content, 20, cancellationToken);
            
            foreach (var similar in similarChunks)
            {
                if (similar.ChunkId == chunk.ChunkId)
                    continue;
                    
                if (similar.SimilarityScore < _similarityThreshold)
                    continue;
                    
                var pairKey = GetPairKey(chunk.ChunkId, similar.ChunkId);
                if (processedPairs.Contains(pairKey))
                    continue;
                    
                processedPairs.Add(pairKey);
                
                var duplicationType = DetermineDuplicationType(chunk.ContentHash, similar.ContentHash, similar.SimilarityScore);
                
                results.Add(new DuplicationResult
                {
                    Type = duplicationType,
                    SimilarityScore = similar.SimilarityScore,
                    Source = new DocumentReference
                    {
                        DocumentId = chunk.DocumentId,
                        FileName = Path.GetFileName(chunk.SourceFile),
                        ChunkId = chunk.ChunkId,
                        PageNumber = chunk.PageNumber,
                        Section = chunk.Section
                    },
                    Target = new DocumentReference
                    {
                        DocumentId = similar.DocumentId,
                        FileName = Path.GetFileName(similar.SourceFile),
                        ChunkId = similar.ChunkId,
                        PageNumber = similar.PageNumber,
                        Section = similar.Section
                    },
                    SourceExcerpt = TruncateText(chunk.Content, 200),
                    TargetExcerpt = TruncateText(similar.Content, 200),
                    Explanation = $"Similarity score: {similar.SimilarityScore:P1}"
                });
            }
        }
        
        return results;
    }
    
    public async Task<List<DuplicationResult>> AnalyzeWithLlmAsync(string? focusArea = null, CancellationToken cancellationToken = default)
    {
        var query = focusArea ?? "Find all duplicate or similar content across all documents";
        
        var (response, _) = await InvokeWithStructuredOutputAsync<DuplicationResponse>(query, cancellationToken: cancellationToken);
        
        if (response?.Duplications == null)
            return new List<DuplicationResult>();
            
        return response.Duplications.Select(d => new DuplicationResult
        {
            Type = ParseDuplicationType(d.Type),
            SimilarityScore = ParseSimilarity(d.Similarity),
            Source = new DocumentReference { FileName = d.SourceFile },
            Target = new DocumentReference { FileName = d.TargetFile },
            SourceExcerpt = d.SourceExcerpt,
            TargetExcerpt = d.TargetExcerpt,
            Explanation = d.Explanation
        }).ToList();
    }
    
    private static string GetPairKey(string id1, string id2)
    {
        return string.CompareOrdinal(id1, id2) < 0 
            ? $"{id1}|{id2}" 
            : $"{id2}|{id1}";
    }
    
    private static DuplicationType DetermineDuplicationType(string hash1, string hash2, double similarity)
    {
        if (hash1 == hash2)
            return DuplicationType.Exact;
        if (similarity >= 0.95)
            return DuplicationType.NearDuplicate;
        return DuplicationType.Semantic;
    }
    
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength - 3) + "...";
    }
    
    private static DuplicationType ParseDuplicationType(string type)
    {
        return type?.ToLowerInvariant() switch
        {
            "exact" => DuplicationType.Exact,
            "near-duplicate" or "nearduplicate" => DuplicationType.NearDuplicate,
            _ => DuplicationType.Semantic
        };
    }
    
    private static double ParseSimilarity(string similarity)
    {
        return similarity?.ToLowerInvariant() switch
        {
            "high" => 0.95,
            "medium" => 0.85,
            "low" => 0.75,
            _ => 0.80
        };
    }
}

internal class DuplicationResponse
{
    public List<DuplicationItem>? Duplications { get; set; }
    public string? Summary { get; set; }
}

internal class DuplicationItem
{
    public string Type { get; set; } = string.Empty;
    public string Similarity { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string TargetFile { get; set; } = string.Empty;
    public string SourceExcerpt { get; set; } = string.Empty;
    public string TargetExcerpt { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

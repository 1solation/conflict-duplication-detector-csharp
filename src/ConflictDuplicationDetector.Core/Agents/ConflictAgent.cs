using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Core.Agents;

public class ConflictAgent : BaseAgent
{
    public ConflictAgent(
        IChatClient chatClient, 
        IVectorStore vectorStore, 
        MetricsTracker metricsTracker) 
        : base(chatClient, vectorStore, metricsTracker, "ConflictAgent")
    {
    }
    
    private static readonly Lazy<string> _systemPrompt = new(() => LoadPromptFromFile("ConflictAgent.txt"));
    protected override string SystemPrompt => _systemPrompt.Value;
    
    public async Task<List<ConflictResult>> AnalyzeAsync(string? topic = null, CancellationToken cancellationToken = default)
    {
        var query = topic ?? "Identify all contradictions, policy conflicts, and inconsistent statements across the documents";
        
        var (response, _) = await InvokeWithStructuredOutputAsync<ConflictResponse>(query, cancellationToken: cancellationToken);
        
        if (response?.Conflicts == null)
            return new List<ConflictResult>();
            
        return response.Conflicts.Select(c => new ConflictResult
        {
            Type = ParseConflictType(c.Type),
            Severity = ParseSeverity(c.Severity),
            Source = new DocumentReference { FileName = c.SourceFile },
            Target = new DocumentReference { FileName = c.TargetFile },
            SourceStatement = c.SourceStatement,
            TargetStatement = c.TargetStatement,
            Explanation = c.Explanation,
            Resolution = c.Resolution
        }).ToList();
    }
    
    public async Task<List<ConflictResult>> AnalyzeTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        var query = $"Find any conflicts or contradictions related to: {topic}";
        return await AnalyzeAsync(query, cancellationToken);
    }
    
    public async Task<List<ConflictResult>> AnalyzePoliciesAsync(CancellationToken cancellationToken = default)
    {
        var query = @"Focus on finding policy-related conflicts such as:
- Conflicting requirements or mandates
- Contradictory rules or procedures
- Inconsistent permissions or restrictions
- Version mismatches in policy documents";
        
        return await AnalyzeAsync(query, cancellationToken);
    }
    
    private static ConflictType ParseConflictType(string type)
    {
        return type?.ToLowerInvariant() switch
        {
            "contradiction" => ConflictType.Contradiction,
            "policy" => ConflictType.PolicyConflict,
            "version" => ConflictType.VersionMismatch,
            "logical" => ConflictType.DataInconsistency,
            _ => ConflictType.Contradiction
        };
    }
    
    private static ConflictSeverity ParseSeverity(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "critical" => ConflictSeverity.Critical,
            "high" => ConflictSeverity.High,
            "medium" => ConflictSeverity.Medium,
            "low" => ConflictSeverity.Low,
            _ => ConflictSeverity.Medium
        };
    }
}

internal class ConflictResponse
{
    public List<ConflictItem>? Conflicts { get; set; }
    public string? Summary { get; set; }
}

internal class ConflictItem
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string TargetFile { get; set; } = string.Empty;
    public string SourceStatement { get; set; } = string.Empty;
    public string TargetStatement { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string? Resolution { get; set; }
}

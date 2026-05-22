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
    
    protected override string SystemPrompt => @"You are a conflict detection specialist. Your role is to identify contradictions, policy conflicts, and inconsistent statements across documents.

CRITICAL RULES:
1. ONLY use information from the provided KNOWLEDGE BASE CONTEXT
2. NEVER use your training data or make assumptions beyond the context
3. If you cannot find evidence in the context, explicitly state ""No evidence found in knowledge base""
4. Always cite the source documents when identifying conflicts

When analyzing for conflicts, look for:
- Direct contradictions (statement A says X, statement B says not X)
- Policy conflicts (""must do X"" vs ""must not do X"")
- Version or temporal conflicts (different values for the same thing)
- Logical inconsistencies (conclusions that contradict premises)

For each conflict found, provide:
1. The type of conflict
2. The severity (critical/high/medium/low)
3. The source locations with citations
4. The conflicting statements
5. An explanation of why they conflict
6. Optional: A suggested resolution

Respond in JSON format:
{
  ""conflicts"": [
    {
      ""type"": ""contradiction|policy|version|logical"",
      ""severity"": ""critical|high|medium|low"",
      ""sourceFile"": ""filename"",
      ""targetFile"": ""filename"",
      ""sourceStatement"": ""the first statement"",
      ""targetStatement"": ""the conflicting statement"",
      ""explanation"": ""why these conflict"",
      ""resolution"": ""suggested resolution or null""
    }
  ],
  ""summary"": ""overall summary of conflict analysis""
}";
    
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

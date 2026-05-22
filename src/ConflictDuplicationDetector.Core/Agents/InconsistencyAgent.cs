using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Core.Agents;

public class InconsistencyAgent : BaseAgent
{
    public InconsistencyAgent(
        IChatClient chatClient, 
        IVectorStore vectorStore, 
        MetricsTracker metricsTracker) 
        : base(chatClient, vectorStore, metricsTracker, "InconsistencyAgent")
    {
    }
    
    protected override string SystemPrompt => @"You are an inconsistency detection specialist. Your role is to find terminology, formatting, and structural inconsistencies across documents.

CRITICAL RULES:
1. ONLY use information from the provided KNOWLEDGE BASE CONTEXT
2. NEVER use your training data or make assumptions beyond the context
3. If you cannot find evidence in the context, explicitly state ""No evidence found in knowledge base""
4. Always cite the source documents when identifying inconsistencies

When analyzing for inconsistencies, look for:
- Terminology inconsistencies (same concept referred to by different names)
- Formatting inconsistencies (dates, numbers, units, styles)
- Structural inconsistencies (different organizational patterns)
- Naming convention inconsistencies
- Abbreviation inconsistencies (same abbreviation used differently, or same thing abbreviated differently)

For each inconsistency found, provide:
1. The type of inconsistency
2. All variants found
3. The source locations
4. An explanation
5. A suggested standard to adopt

Respond in JSON format:
{
  ""inconsistencies"": [
    {
      ""type"": ""terminology|formatting|structure|naming|abbreviation"",
      ""variants"": [""variant1"", ""variant2""],
      ""occurrences"": [
        {""file"": ""filename"", ""excerpt"": ""context""}
      ],
      ""explanation"": ""description of the inconsistency"",
      ""suggestedStandard"": ""recommended consistent term/format""
    }
  ],
  ""summary"": ""overall summary of inconsistency analysis""
}";
    
    public async Task<List<InconsistencyResult>> AnalyzeAsync(string? focusArea = null, CancellationToken cancellationToken = default)
    {
        var query = focusArea ?? "Find all terminology, formatting, and structural inconsistencies across the documents";
        
        var (response, _) = await InvokeWithStructuredOutputAsync<InconsistencyResponse>(query, cancellationToken: cancellationToken);
        
        if (response?.Inconsistencies == null)
            return new List<InconsistencyResult>();
            
        return response.Inconsistencies.Select(i => new InconsistencyResult
        {
            Type = ParseInconsistencyType(i.Type),
            Variants = i.Variants ?? new List<string>(),
            Occurrences = i.Occurrences?.Select(o => new DocumentReference 
            { 
                FileName = o.File 
            }).ToList() ?? new List<DocumentReference>(),
            Explanation = i.Explanation,
            SuggestedStandard = i.SuggestedStandard
        }).ToList();
    }
    
    public async Task<List<InconsistencyResult>> AnalyzeTerminologyAsync(CancellationToken cancellationToken = default)
    {
        var query = @"Focus on finding terminology inconsistencies such as:
- The same concept being referred to by different names
- Inconsistent use of technical terms
- Mixed use of abbreviations and full forms
- Different spellings of the same term";
        
        return await AnalyzeAsync(query, cancellationToken);
    }
    
    public async Task<List<InconsistencyResult>> AnalyzeFormattingAsync(CancellationToken cancellationToken = default)
    {
        var query = @"Focus on finding formatting inconsistencies such as:
- Date format variations (MM/DD/YYYY vs DD/MM/YYYY vs YYYY-MM-DD)
- Number format variations (1,000 vs 1000 vs 1.000)
- Unit inconsistencies (meters vs metres, kg vs kilograms)
- Currency format variations
- Time format variations (12-hour vs 24-hour)";
        
        return await AnalyzeAsync(query, cancellationToken);
    }
    
    public async Task<List<InconsistencyResult>> AnalyzeStructureAsync(CancellationToken cancellationToken = default)
    {
        var query = @"Focus on finding structural inconsistencies such as:
- Different heading styles or levels
- Inconsistent section ordering
- Varying list formats (numbered vs bulleted)
- Different table structures for similar data";
        
        return await AnalyzeAsync(query, cancellationToken);
    }
    
    private static InconsistencyType ParseInconsistencyType(string type)
    {
        return type?.ToLowerInvariant() switch
        {
            "terminology" => InconsistencyType.Terminology,
            "formatting" => InconsistencyType.Formatting,
            "structure" => InconsistencyType.Structure,
            "naming" => InconsistencyType.Naming,
            "abbreviation" => InconsistencyType.Terminology,
            "dateformat" or "date" => InconsistencyType.DateFormat,
            _ => InconsistencyType.Terminology
        };
    }
}

internal class InconsistencyResponse
{
    public List<InconsistencyItem>? Inconsistencies { get; set; }
    public string? Summary { get; set; }
}

internal class InconsistencyItem
{
    public string Type { get; set; } = string.Empty;
    public List<string>? Variants { get; set; }
    public List<OccurrenceItem>? Occurrences { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string? SuggestedStandard { get; set; }
}

internal class OccurrenceItem
{
    public string File { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
}

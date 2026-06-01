using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Core.Services;

public class FileAnalysisService
{
    private readonly IChatClient _chatClient;
    private readonly IVectorStore _vectorStore;
    private readonly MetricsTracker _metricsTracker;
    private readonly AnalysisConfiguration _config;

    public FileAnalysisService(
        IChatClient chatClient,
        IVectorStore vectorStore,
        MetricsTracker metricsTracker,
        AnalysisConfiguration config)
    {
        _chatClient = chatClient;
        _vectorStore = vectorStore;
        _metricsTracker = metricsTracker;
        _config = config;
    }

    public async Task<AnalysisResult> AnalyzeFileAsync(
        List<DocumentChunk> fileChunks,
        Document document,
        string analysisType = "all",
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new AnalysisResult();

        var tasks = new List<Task>();

        if (analysisType is "all" or "duplications" or "duplication")
        {
            var dupTask = FindDuplicationsAsync(fileChunks, cancellationToken);
            tasks.Add(dupTask.ContinueWith(t => result.Duplications = t.Result, cancellationToken));
        }

        if (analysisType is "all" or "conflicts" or "conflict")
        {
            var conflictTask = FindConflictsAsync(fileChunks, document, cancellationToken);
            tasks.Add(conflictTask.ContinueWith(t => result.Conflicts = t.Result, cancellationToken));
        }

        if (analysisType is "all" or "inconsistencies" or "inconsistency")
        {
            var inconsistencyTask = FindInconsistenciesAsync(fileChunks, document, cancellationToken);
            tasks.Add(inconsistencyTask.ContinueWith(t => result.Inconsistencies = t.Result, cancellationToken));
        }

        await Task.WhenAll(tasks);

        result.Metrics = new AnalysisMetrics
        {
            TotalChunks = fileChunks.Count,
            DuplicationsFound = result.Duplications.Count,
            ConflictsFound = result.Conflicts.Count,
            InconsistenciesFound = result.Inconsistencies.Count,
            TotalDuration = DateTime.UtcNow - startTime,
            AgentMetrics = _metricsTracker.GetAllMetrics()
        };

        return result;
    }

    private async Task<List<DuplicationResult>> FindDuplicationsAsync(
        List<DocumentChunk> fileChunks,
        CancellationToken cancellationToken)
    {
        var results = new List<DuplicationResult>();
        var sourceFile = fileChunks.FirstOrDefault()?.SourceFile ?? string.Empty;

        foreach (var chunk in fileChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(chunk.Content))
                continue;

            var similarChunks = await _vectorStore.SearchAsync(chunk.Content, 10, cancellationToken);

            foreach (var similar in similarChunks)
            {
                if (similar.SourceFile == sourceFile)
                    continue;

                if (similar.SimilarityScore < _config.DuplicationThreshold)
                    continue;

                var duplicationType = DetermineDuplicationType(chunk.ContentHash, similar.ContentHash, similar.SimilarityScore);

                results.Add(new DuplicationResult
                {
                    Type = duplicationType,
                    SimilarityScore = similar.SimilarityScore,
                    Source = new DocumentReference
                    {
                        DocumentId = chunk.DocumentId,
                        FileName = Path.GetFileName(chunk.SourceFile),
                        ChunkId = chunk.Id,
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

    private async Task<List<ConflictResult>> FindConflictsAsync(
        List<DocumentChunk> fileChunks,
        Document document,
        CancellationToken cancellationToken)
    {
        var metric = _metricsTracker.StartCall("FileConflictAgent", "analyze_file");
        var calcStopwatch = Stopwatch.StartNew();

        var fileContent = BuildFileContext(fileChunks);
        var kbContext = await GetRelevantKnowledgeBaseContext(fileChunks, cancellationToken);
        calcStopwatch.Stop();

        var systemPrompt = @"You are a conflict detection specialist. Your role is to identify contradictions, policy conflicts, and inconsistent statements between a TARGET FILE and the KNOWLEDGE BASE.

CRITICAL RULES:
1. Compare the TARGET FILE content against the KNOWLEDGE BASE content
2. Only report genuine conflicts where the file contradicts or disagrees with knowledge base documents
3. For each conflict, cite both sources clearly

For each conflict found, provide:
1. The type of conflict (contradiction, policy, version, logical)
2. The severity (critical/high/medium/low)
3. The file containing the statement
4. The knowledge base document containing the conflicting statement
5. An explanation of why they conflict
6. Optional: A suggested resolution

Respond in JSON format:
{
  ""conflicts"": [
    {
      ""type"": ""contradiction|policy|version|logical"",
      ""severity"": ""critical|high|medium|low"",
      ""sourceFile"": ""target filename"",
      ""targetFile"": ""knowledge base filename"",
      ""sourceStatement"": ""statement from target file"",
      ""targetStatement"": ""conflicting statement from knowledge base"",
      ""explanation"": ""why these conflict"",
      ""resolution"": ""suggested resolution or null""
    }
  ],
  ""summary"": ""overall summary""
}";

        var userMessage = $@"=== TARGET FILE: {document.FileName} ===
{fileContent}

=== KNOWLEDGE BASE CONTEXT ===
{kbContext}

Find all contradictions, policy conflicts, and inconsistent statements between the target file and the knowledge base documents.";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };

        var networkStopwatch = Stopwatch.StartNew();
        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        networkStopwatch.Stop();

        var responseText = response.Text ?? string.Empty;
        var inputTokens = EstimateTokens(systemPrompt + userMessage);
        var outputTokens = EstimateTokens(responseText);

        _metricsTracker.CompleteCall(metric,
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            networkTimeMs: networkStopwatch.ElapsedMilliseconds,
            calculationTimeMs: calcStopwatch.ElapsedMilliseconds,
            success: true);

        return ParseConflictResponse(responseText);
    }

    private async Task<List<InconsistencyResult>> FindInconsistenciesAsync(
        List<DocumentChunk> fileChunks,
        Document document,
        CancellationToken cancellationToken)
    {
        var metric = _metricsTracker.StartCall("FileInconsistencyAgent", "analyze_file");
        var calcStopwatch = Stopwatch.StartNew();

        var fileContent = BuildFileContext(fileChunks);
        var kbContext = await GetRelevantKnowledgeBaseContext(fileChunks, cancellationToken);
        calcStopwatch.Stop();

        var systemPrompt = @"You are an inconsistency detection specialist. Your role is to find terminology, formatting, and structural inconsistencies between a TARGET FILE and the KNOWLEDGE BASE.

CRITICAL RULES:
1. Compare the TARGET FILE content against the KNOWLEDGE BASE content
2. Look for inconsistencies in terminology, formatting, dates, naming, and structure
3. For each inconsistency, cite where each variant appears

When analyzing, look for:
- Same concept referred to differently between the file and knowledge base
- Formatting differences (dates, numbers, units)
- Structural inconsistencies
- Naming convention differences
- Abbreviation inconsistencies

Respond in JSON format:
{
  ""inconsistencies"": [
    {
      ""type"": ""terminology|formatting|structure|naming|abbreviation"",
      ""variants"": [""variant in file"", ""variant in knowledge base""],
      ""occurrences"": [
        {""file"": ""filename"", ""excerpt"": ""context""}
      ],
      ""explanation"": ""description of the inconsistency"",
      ""suggestedStandard"": ""recommended consistent term/format""
    }
  ],
  ""summary"": ""overall summary""
}";

        var userMessage = $@"=== TARGET FILE: {document.FileName} ===
{fileContent}

=== KNOWLEDGE BASE CONTEXT ===
{kbContext}

Find all terminology, formatting, and structural inconsistencies between the target file and the knowledge base documents.";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };

        var networkStopwatch = Stopwatch.StartNew();
        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        networkStopwatch.Stop();

        var responseText = response.Text ?? string.Empty;
        var inputTokens = EstimateTokens(systemPrompt + userMessage);
        var outputTokens = EstimateTokens(responseText);

        _metricsTracker.CompleteCall(metric,
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            networkTimeMs: networkStopwatch.ElapsedMilliseconds,
            calculationTimeMs: calcStopwatch.ElapsedMilliseconds,
            success: true);

        return ParseInconsistencyResponse(responseText);
    }

    private async Task<string> GetRelevantKnowledgeBaseContext(
        List<DocumentChunk> fileChunks,
        CancellationToken cancellationToken)
    {
        var contextBuilder = new StringBuilder();
        var seenContent = new HashSet<string>();

        foreach (var chunk in fileChunks.Take(10))
        {
            if (string.IsNullOrWhiteSpace(chunk.Content))
                continue;

            var results = await _vectorStore.SearchAsync(chunk.Content, 5, cancellationToken);

            foreach (var result in results)
            {
                if (seenContent.Contains(result.ChunkId))
                    continue;

                seenContent.Add(result.ChunkId);
                contextBuilder.AppendLine($"[Source: {Path.GetFileName(result.SourceFile)}]");
                if (!string.IsNullOrEmpty(result.Section))
                    contextBuilder.AppendLine($"[Section: {result.Section}]");
                if (!string.IsNullOrEmpty(result.PageNumber))
                    contextBuilder.AppendLine($"[Page: {result.PageNumber}]");
                contextBuilder.AppendLine(result.Content);
                contextBuilder.AppendLine("---");
            }
        }

        return contextBuilder.ToString();
    }

    private static string BuildFileContext(List<DocumentChunk> fileChunks)
    {
        var builder = new StringBuilder();
        foreach (var chunk in fileChunks)
        {
            if (!string.IsNullOrEmpty(chunk.Section))
                builder.AppendLine($"[Section: {chunk.Section}]");
            if (!string.IsNullOrEmpty(chunk.PageNumber))
                builder.AppendLine($"[Page: {chunk.PageNumber}]");
            builder.AppendLine(chunk.Content);
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static List<ConflictResult> ParseConflictResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
                return new List<ConflictResult>();

            var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var parsed = JsonSerializer.Deserialize<FileConflictResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed?.Conflicts == null)
                return new List<ConflictResult>();

            return parsed.Conflicts.Select(c => new ConflictResult
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
        catch (JsonException)
        {
            return new List<ConflictResult>();
        }
    }

    private static List<InconsistencyResult> ParseInconsistencyResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
                return new List<InconsistencyResult>();

            var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var parsed = JsonSerializer.Deserialize<FileInconsistencyResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed?.Inconsistencies == null)
                return new List<InconsistencyResult>();

            return parsed.Inconsistencies.Select(i => new InconsistencyResult
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
        catch (JsonException)
        {
            return new List<InconsistencyResult>();
        }
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

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
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

internal class FileConflictResponse
{
    public List<FileConflictItem>? Conflicts { get; set; }
    public string? Summary { get; set; }
}

internal class FileConflictItem
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

internal class FileInconsistencyResponse
{
    public List<FileInconsistencyItem>? Inconsistencies { get; set; }
    public string? Summary { get; set; }
}

internal class FileInconsistencyItem
{
    public string Type { get; set; } = string.Empty;
    public List<string>? Variants { get; set; }
    public List<FileOccurrenceItem>? Occurrences { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string? SuggestedStandard { get; set; }
}

internal class FileOccurrenceItem
{
    public string File { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
}

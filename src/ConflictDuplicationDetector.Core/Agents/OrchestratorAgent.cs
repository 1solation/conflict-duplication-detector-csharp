using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Core.Agents;

public class OrchestratorAgent : BaseAgent
{
    private readonly DuplicationAgent _duplicationAgent;
    private readonly ConflictAgent _conflictAgent;
    private readonly InconsistencyAgent _inconsistencyAgent;

    public OrchestratorAgent(
        IChatClient chatClient,
        IVectorStore vectorStore,
        MetricsTracker metricsTracker,
        double duplicationThreshold = 0.85)
        : base(chatClient, vectorStore, metricsTracker, "OrchestratorAgent")
    {
        _duplicationAgent = new DuplicationAgent(chatClient, vectorStore, metricsTracker, duplicationThreshold);
        _conflictAgent = new ConflictAgent(chatClient, vectorStore, metricsTracker);
        _inconsistencyAgent = new InconsistencyAgent(chatClient, vectorStore, metricsTracker);
    }

    private static readonly Lazy<string> _systemPrompt = new(() => LoadPromptFromFile("OrchestratorAgent.txt"));
    protected override string SystemPrompt => _systemPrompt.Value;

    public async Task<AnalysisResult> RunFullAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new AnalysisResult();

        var duplicationsTask = _duplicationAgent.AnalyseAsync(cancellationToken);
        var conflictsTask = _conflictAgent.AnalyseAsync(cancellationToken: cancellationToken);
        var inconsistenciesTask = _inconsistencyAgent.AnalyseAsync(cancellationToken: cancellationToken);

        await Task.WhenAll(duplicationsTask, conflictsTask, inconsistenciesTask);

        result.Duplications = await duplicationsTask;
        result.Conflicts = await conflictsTask;
        result.Inconsistencies = await inconsistenciesTask;

        result.Metrics = new AnalysisMetrics
        {
            TotalChunks = await VectorStore.GetChunkCountAsync(cancellationToken),
            DuplicationsFound = result.Duplications.Count,
            ConflictsFound = result.Conflicts.Count,
            InconsistenciesFound = result.Inconsistencies.Count,
            TotalDuration = DateTime.UtcNow - startTime,
            AgentMetrics = MetricsTracker.GetAllMetrics()
        };

        return result;
    }

    public async Task<AnalysisResult> RunDuplicationAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new AnalysisResult
        {
            Duplications = await _duplicationAgent.AnalyseAsync(cancellationToken)
        };

        result.Metrics = new AnalysisMetrics
        {
            TotalChunks = await VectorStore.GetChunkCountAsync(cancellationToken),
            DuplicationsFound = result.Duplications.Count,
            TotalDuration = DateTime.UtcNow - startTime,
            AgentMetrics = MetricsTracker.GetAllMetrics()
        };

        return result;
    }

    public async Task<AnalysisResult> RunConflictAnalysisAsync(string? topic = null, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new AnalysisResult
        {
            Conflicts = await _conflictAgent.AnalyseAsync(topic, cancellationToken)
        };

        result.Metrics = new AnalysisMetrics
        {
            TotalChunks = await VectorStore.GetChunkCountAsync(cancellationToken),
            ConflictsFound = result.Conflicts.Count,
            TotalDuration = DateTime.UtcNow - startTime,
            AgentMetrics = MetricsTracker.GetAllMetrics()
        };

        return result;
    }

    public async Task<AnalysisResult> RunInconsistencyAnalysisAsync(string? focusArea = null, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new AnalysisResult
        {
            Inconsistencies = await _inconsistencyAgent.AnalyseAsync(focusArea, cancellationToken)
        };

        result.Metrics = new AnalysisMetrics
        {
            TotalChunks = await VectorStore.GetChunkCountAsync(cancellationToken),
            InconsistenciesFound = result.Inconsistencies.Count,
            TotalDuration = DateTime.UtcNow - startTime,
            AgentMetrics = MetricsTracker.GetAllMetrics()
        };

        return result;
    }

    public async Task<ChatResponse> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        var intent = await ClassifyIntentAsync(userMessage, cancellationToken);

        return intent switch
        {
            UserIntent.Duplication => await HandleDuplicationQueryAsync(userMessage, cancellationToken),
            UserIntent.Conflict => await HandleConflictQueryAsync(userMessage, cancellationToken),
            UserIntent.Inconsistency => await HandleInconsistencyQueryAsync(userMessage, cancellationToken),
            UserIntent.FullAnalysis => await HandleFullAnalysisQueryAsync(cancellationToken),
            _ => await HandleGeneralQueryAsync(userMessage, cancellationToken)
        };
    }

    private async Task<UserIntent> ClassifyIntentAsync(string userMessage, CancellationToken cancellationToken)
    {
        var lowerMessage = userMessage.ToLowerInvariant();

        if (lowerMessage.Contains("duplicate") || lowerMessage.Contains("copy") || lowerMessage.Contains("same content") || lowerMessage.Contains("similar"))
            return UserIntent.Duplication;

        if (lowerMessage.Contains("conflict") || lowerMessage.Contains("contradict") || lowerMessage.Contains("disagree") || lowerMessage.Contains("opposite"))
            return UserIntent.Conflict;

        if (lowerMessage.Contains("inconsisten") || lowerMessage.Contains("terminology") || lowerMessage.Contains("format") || lowerMessage.Contains("style"))
            return UserIntent.Inconsistency;

        if (lowerMessage.Contains("full analysis") || lowerMessage.Contains("analyse all") || lowerMessage.Contains("complete analysis"))
            return UserIntent.FullAnalysis;

        return UserIntent.General;
    }

    private async Task<ChatResponse> HandleDuplicationQueryAsync(string query, CancellationToken cancellationToken)
    {
        var duplications = await _duplicationAgent.AnalyseWithLlmAsync(query, cancellationToken);

        return new ChatResponse
        {
            Message = FormatDuplicationResponse(duplications),
            Intent = UserIntent.Duplication,
            ResultCount = duplications.Count
        };
    }

    private async Task<ChatResponse> HandleConflictQueryAsync(string query, CancellationToken cancellationToken)
    {
        var conflicts = await _conflictAgent.AnalyseAsync(query, cancellationToken);

        return new ChatResponse
        {
            Message = FormatConflictResponse(conflicts),
            Intent = UserIntent.Conflict,
            ResultCount = conflicts.Count
        };
    }

    private async Task<ChatResponse> HandleInconsistencyQueryAsync(string query, CancellationToken cancellationToken)
    {
        var inconsistencies = await _inconsistencyAgent.AnalyseAsync(query, cancellationToken);

        return new ChatResponse
        {
            Message = FormatInconsistencyResponse(inconsistencies),
            Intent = UserIntent.Inconsistency,
            ResultCount = inconsistencies.Count
        };
    }

    private async Task<ChatResponse> HandleFullAnalysisQueryAsync(CancellationToken cancellationToken)
    {
        var result = await RunFullAnalysisAsync(cancellationToken);

        return new ChatResponse
        {
            Message = FormatFullAnalysisResponse(result),
            Intent = UserIntent.FullAnalysis,
            ResultCount = result.Duplications.Count + result.Conflicts.Count + result.Inconsistencies.Count
        };
    }

    private async Task<ChatResponse> HandleGeneralQueryAsync(string query, CancellationToken cancellationToken)
    {
        var (response, _) = await InvokeAsync(query, cancellationToken: cancellationToken);

        return new ChatResponse
        {
            Message = response,
            Intent = UserIntent.General,
            ResultCount = 0
        };
    }

    private static string FormatDuplicationResponse(List<DuplicationResult> duplications)
    {
        if (!duplications.Any())
            return "No duplications found in the knowledge base.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {duplications.Count} potential duplication(s):\n");

        foreach (var dup in duplications)
        {
            sb.AppendLine($"**{dup.Type} Duplication** (Similarity: {dup.SimilarityScore:P0})");
            sb.AppendLine($"- Source: {dup.Source.FileName}");
            sb.AppendLine($"- Target: {dup.Target.FileName}");
            sb.AppendLine($"- Explanation: {dup.Explanation}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatConflictResponse(List<ConflictResult> conflicts)
    {
        if (!conflicts.Any())
            return "No conflicts found in the knowledge base.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {conflicts.Count} potential conflict(s):\n");

        foreach (var conflict in conflicts)
        {
            sb.AppendLine($"**{conflict.Type}** (Severity: {conflict.Severity})");
            sb.AppendLine($"- Source: {conflict.Source.FileName} - \"{conflict.SourceStatement}\"");
            sb.AppendLine($"- Target: {conflict.Target.FileName} - \"{conflict.TargetStatement}\"");
            sb.AppendLine($"- Explanation: {conflict.Explanation}");
            if (!string.IsNullOrEmpty(conflict.Resolution))
                sb.AppendLine($"- Suggested Resolution: {conflict.Resolution}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatInconsistencyResponse(List<InconsistencyResult> inconsistencies)
    {
        if (!inconsistencies.Any())
            return "No inconsistencies found in the knowledge base.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {inconsistencies.Count} inconsistency type(s):\n");

        foreach (var inconsistency in inconsistencies)
        {
            sb.AppendLine($"**{inconsistency.Type} Inconsistency**");
            sb.AppendLine($"- Variants: {string.Join(", ", inconsistency.Variants)}");
            sb.AppendLine($"- Found in: {string.Join(", ", inconsistency.Occurrences.Select(o => o.FileName))}");
            sb.AppendLine($"- Explanation: {inconsistency.Explanation}");
            if (!string.IsNullOrEmpty(inconsistency.SuggestedStandard))
                sb.AppendLine($"- Suggested Standard: {inconsistency.SuggestedStandard}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatFullAnalysisResponse(AnalysisResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Full Document Analysis Report\n");
        sb.AppendLine($"Analysis completed in {result.Metrics.TotalDuration.TotalSeconds:F1} seconds\n");
        sb.AppendLine($"## Summary");
        sb.AppendLine($"- Documents analysed: {result.Metrics.TotalDocuments}");
        sb.AppendLine($"- Chunks processed: {result.Metrics.TotalChunks}");
        sb.AppendLine($"- Duplications found: {result.Duplications.Count}");
        sb.AppendLine($"- Conflicts found: {result.Conflicts.Count}");
        sb.AppendLine($"- Inconsistencies found: {result.Inconsistencies.Count}");
        sb.AppendLine();

        if (result.Duplications.Any())
        {
            sb.AppendLine("## Duplications");
            sb.AppendLine(FormatDuplicationResponse(result.Duplications));
        }

        if (result.Conflicts.Any())
        {
            sb.AppendLine("## Conflicts");
            sb.AppendLine(FormatConflictResponse(result.Conflicts));
        }

        if (result.Inconsistencies.Any())
        {
            sb.AppendLine("## Inconsistencies");
            sb.AppendLine(FormatInconsistencyResponse(result.Inconsistencies));
        }

        return sb.ToString();
    }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public UserIntent Intent { get; set; }
    public int ResultCount { get; set; }
}

public enum UserIntent
{
    General,
    Duplication,
    Conflict,
    Inconsistency,
    FullAnalysis
}

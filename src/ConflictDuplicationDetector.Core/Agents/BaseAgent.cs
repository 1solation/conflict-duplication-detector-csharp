using System.Diagnostics;
using System.Text.Json;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Core.Agents;

public abstract class BaseAgent
{
    protected readonly IChatClient ChatClient;
    protected readonly IVectorStore VectorStore;
    protected readonly MetricsTracker MetricsTracker;
    protected readonly string AgentName;
    
    protected BaseAgent(IChatClient chatClient, IVectorStore vectorStore, MetricsTracker metricsTracker, string agentName)
    {
        ChatClient = chatClient;
        VectorStore = vectorStore;
        MetricsTracker = metricsTracker;
        AgentName = agentName;
    }
    
    protected abstract string SystemPrompt { get; }
    
    protected async Task<string> GetContextAsync(string query, int topK = 10, CancellationToken cancellationToken = default)
    {
        var results = await VectorStore.SearchAsync(query, topK, cancellationToken);
        
        if (!results.Any())
            return "No relevant documents found in the knowledge base.";
            
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("=== KNOWLEDGE BASE CONTEXT ===");
        contextBuilder.AppendLine();
        
        foreach (var result in results)
        {
            contextBuilder.AppendLine($"[Source: {result.SourceFile}]");
            if (!string.IsNullOrEmpty(result.PageNumber))
                contextBuilder.AppendLine($"[Page: {result.PageNumber}]");
            contextBuilder.AppendLine($"[Relevance: {result.SimilarityScore:P0}]");
            contextBuilder.AppendLine(result.Content);
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("---");
            contextBuilder.AppendLine();
        }
        
        contextBuilder.AppendLine("=== END CONTEXT ===");
        return contextBuilder.ToString();
    }
    
    protected async Task<(string Response, AgentCallMetric Metric)> InvokeAsync(
        string userMessage, 
        string? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var metric = MetricsTracker.StartCall(AgentName, "invoke");
        var calcStopwatch = Stopwatch.StartNew();
        
        try
        {
            var context = await GetContextAsync(userMessage, cancellationToken: cancellationToken);
            calcStopwatch.Stop();
            var calculationTime = calcStopwatch.ElapsedMilliseconds;
            
            var fullContext = additionalContext != null 
                ? $"{context}\n\n{additionalContext}" 
                : context;
            
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, $"{fullContext}\n\nUser Query: {userMessage}")
            };
            
            var networkStopwatch = Stopwatch.StartNew();
            var response = await ChatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            networkStopwatch.Stop();
            
            var responseText = response.Text ?? string.Empty;
            var inputTokens = EstimateTokens(SystemPrompt + fullContext + userMessage);
            var outputTokens = EstimateTokens(responseText);
            
            MetricsTracker.CompleteCall(metric, 
                inputTokens: inputTokens,
                outputTokens: outputTokens,
                networkTimeMs: networkStopwatch.ElapsedMilliseconds,
                calculationTimeMs: calculationTime,
                success: true);
            
            return (responseText, metric);
        }
        catch (Exception ex)
        {
            MetricsTracker.CompleteCall(metric, success: false, error: ex.Message);
            throw;
        }
    }
    
    protected async Task<(T? Result, AgentCallMetric Metric)> InvokeWithStructuredOutputAsync<T>(
        string userMessage,
        string? additionalContext = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var (response, metric) = await InvokeAsync(userMessage, additionalContext, cancellationToken);
        
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                return (result, metric);
            }
            
            var arrayStart = response.IndexOf('[');
            var arrayEnd = response.LastIndexOf(']');
            
            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                var json = response.Substring(arrayStart, arrayEnd - arrayStart + 1);
                var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                return (result, metric);
            }
        }
        catch (JsonException)
        {
        }
        
        return (null, metric);
    }
    
    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}

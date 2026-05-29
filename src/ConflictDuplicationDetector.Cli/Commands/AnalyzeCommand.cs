using System.CommandLine;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ConflictDuplicationDetector.Cli.Commands;

public class AnalyzeCommand : Command
{
    public AnalyzeCommand() : base("analyze", "Analyze documents for conflicts, duplications, and inconsistencies")
    {
        var typeOption = new Option<string?>("--type", "Analysis type: all, duplications, conflicts, inconsistencies");
        var topicOption = new Option<string?>("--topic", "Focus topic for analysis");
        var outputOption = new Option<string?>("--output", "Output file path for results (JSON)");
        var configOption = new Option<string?>("--config", "Path to configuration file");
        
        AddOption(typeOption);
        AddOption(topicOption);
        AddOption(outputOption);
        AddOption(configOption);
        
        this.SetHandler(ExecuteAsync, typeOption, topicOption, outputOption, configOption);
    }
    
    private async Task ExecuteAsync(string? type, string? topic, string? outputPath, string? configPath)
    {
        Console.WriteLine("Document Analysis");
        Console.WriteLine("=================");
        
        var config = ConfigurationLoader.Load(configPath);
        
        if (string.IsNullOrEmpty(config.OpenAI.ApiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: OpenAI API key not configured. Set OPENAI_API_KEY environment variable or configure in appsettings.json");
            Console.ResetColor();
            return;
        }
        
        var vectorStore = new SharpVectorStore(config.OpenAI.ApiKey, config.OpenAI.EmbeddingModel);
        
        if (!File.Exists(config.VectorStore.PersistPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No documents have been ingested yet. Run 'ingest' command first.");
            Console.ResetColor();
            return;
        }
        
        await vectorStore.LoadAsync(config.VectorStore.PersistPath);
        var chunkCount = await vectorStore.GetChunkCountAsync();
        
        if (chunkCount == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Vector store is empty. Run 'ingest' command first.");
            Console.ResetColor();
            return;
        }
        
        Console.WriteLine($"Loaded {chunkCount} document chunks from vector store.");
        Console.WriteLine();
        
        var openAiClient = new OpenAIClient(config.OpenAI.ApiKey);
        var chatClient = openAiClient.GetChatClient(config.OpenAI.Model).AsIChatClient();
        var metricsTracker = new MetricsTracker();
        
        var analysisService = new AnalysisService(
            chatClient, 
            vectorStore, 
            metricsTracker, 
            config.Analysis, 
            config.VectorStore.PersistPath);
        
        var analysisType = type?.ToLowerInvariant() ?? "all";
        
        Console.WriteLine($"Running {analysisType} analysis...");
        Console.WriteLine();
        
        AnalysisResult result;
        
        switch (analysisType)
        {
            case "duplications":
            case "duplication":
                result = await analysisService.RunDuplicationAnalysisAsync();
                break;
            case "conflicts":
            case "conflict":
                result = await analysisService.RunConflictAnalysisAsync(topic);
                break;
            case "inconsistencies":
            case "inconsistency":
                result = await analysisService.RunInconsistencyAnalysisAsync(topic);
                break;
            default:
                result = await analysisService.RunFullAnalysisAsync();
                break;
        }
        
        PrintResults(result);
        PrintMetrics(result.Metrics);
        
        if (!string.IsNullOrEmpty(outputPath))
        {
            await analysisService.SaveResultsAsync(result, outputPath);
            Console.WriteLine($"\nResults saved to: {outputPath}");
        }
    }
    
    private static void PrintResults(AnalysisResult result)
    {
        Console.WriteLine("Analysis Results");
        Console.WriteLine("----------------");
        
        if (result.Duplications.Any())
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nDuplications Found: {result.Duplications.Count}");
            Console.ResetColor();
            
            foreach (var dup in result.Duplications.Take(10))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  [{dup.Type}] Similarity: {dup.SimilarityScore:P0}");
                Console.ResetColor();
                
                Console.WriteLine($"    Source: {dup.Source.FileName}");
                PrintLocation(dup.Source);
                if (!string.IsNullOrEmpty(dup.SourceExcerpt))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      \"{TruncateForDisplay(dup.SourceExcerpt, 100)}\"");
                    Console.ResetColor();
                }
                
                Console.WriteLine($"    Target: {dup.Target.FileName}");
                PrintLocation(dup.Target);
                if (!string.IsNullOrEmpty(dup.TargetExcerpt))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      \"{TruncateForDisplay(dup.TargetExcerpt, 100)}\"");
                    Console.ResetColor();
                }
            }
            
            if (result.Duplications.Count > 10)
                Console.WriteLine($"\n  ... and {result.Duplications.Count - 10} more");
        }
        else
        {
            Console.WriteLine("\nNo duplications found.");
        }
        
        if (result.Conflicts.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nConflicts Found: {result.Conflicts.Count}");
            Console.ResetColor();
            
            foreach (var conflict in result.Conflicts.Take(10))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  [{conflict.Severity}] {conflict.Type}");
                Console.ResetColor();
                
                Console.WriteLine($"    Source: {conflict.Source.FileName}");
                PrintLocation(conflict.Source);
                if (!string.IsNullOrEmpty(conflict.SourceStatement))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      \"{TruncateForDisplay(conflict.SourceStatement, 100)}\"");
                    Console.ResetColor();
                }
                
                Console.WriteLine($"    Target: {conflict.Target.FileName}");
                PrintLocation(conflict.Target);
                if (!string.IsNullOrEmpty(conflict.TargetStatement))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      \"{TruncateForDisplay(conflict.TargetStatement, 100)}\"");
                    Console.ResetColor();
                }
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"    Explanation: {conflict.Explanation}");
                Console.ResetColor();
                
                if (!string.IsNullOrEmpty(conflict.Resolution))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    Resolution: {conflict.Resolution}");
                    Console.ResetColor();
                }
            }
            
            if (result.Conflicts.Count > 10)
                Console.WriteLine($"\n  ... and {result.Conflicts.Count - 10} more");
        }
        else
        {
            Console.WriteLine("\nNo conflicts found.");
        }
        
        if (result.Inconsistencies.Any())
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\nInconsistencies Found: {result.Inconsistencies.Count}");
            Console.ResetColor();
            
            foreach (var inconsistency in result.Inconsistencies.Take(10))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  [{inconsistency.Type}]");
                Console.ResetColor();
                
                Console.WriteLine($"    Variants: {string.Join(", ", inconsistency.Variants.Take(5))}");
                
                if (inconsistency.Occurrences.Any())
                {
                    Console.WriteLine("    Found in:");
                    foreach (var occurrence in inconsistency.Occurrences.Take(5))
                    {
                        Console.Write($"      - {occurrence.FileName}");
                        var locationParts = new List<string>();
                        if (!string.IsNullOrEmpty(occurrence.PageNumber))
                            locationParts.Add($"Page {occurrence.PageNumber}");
                        if (!string.IsNullOrEmpty(occurrence.Section))
                            locationParts.Add($"Section: {occurrence.Section}");
                        if (locationParts.Any())
                            Console.Write($" ({string.Join(", ", locationParts)})");
                        Console.WriteLine();
                    }
                    if (inconsistency.Occurrences.Count > 5)
                        Console.WriteLine($"      ... and {inconsistency.Occurrences.Count - 5} more locations");
                }
                
                if (!string.IsNullOrEmpty(inconsistency.Explanation))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"    Explanation: {inconsistency.Explanation}");
                    Console.ResetColor();
                }
                
                if (!string.IsNullOrEmpty(inconsistency.SuggestedStandard))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    Suggested: {inconsistency.SuggestedStandard}");
                    Console.ResetColor();
                }
            }
            
            if (result.Inconsistencies.Count > 10)
                Console.WriteLine($"\n  ... and {result.Inconsistencies.Count - 10} more");
        }
        else
        {
            Console.WriteLine("\nNo inconsistencies found.");
        }
    }
    
    private static void PrintLocation(DocumentReference reference)
    {
        var locationParts = new List<string>();
        
        if (!string.IsNullOrEmpty(reference.PageNumber))
            locationParts.Add($"Page {reference.PageNumber}");
        if (!string.IsNullOrEmpty(reference.Section))
            locationParts.Add($"Section: {reference.Section}");
        if (reference.LineNumber.HasValue)
            locationParts.Add($"Line {reference.LineNumber}");
            
        if (locationParts.Any())
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"      Location: {string.Join(", ", locationParts)}");
            Console.ResetColor();
        }
    }
    
    private static string TruncateForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
            
        var cleaned = text.Replace("\r", " ").Replace("\n", " ");
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        cleaned = cleaned.Trim();
        
        if (cleaned.Length <= maxLength)
            return cleaned;
            
        return cleaned.Substring(0, maxLength - 3) + "...";
    }
    
    private static void PrintMetrics(AnalysisMetrics metrics)
    {
        Console.WriteLine();
        Console.WriteLine("Performance Metrics");
        Console.WriteLine("-------------------");
        Console.WriteLine($"Total Duration: {metrics.TotalDuration.TotalSeconds:F2}s");
        Console.WriteLine($"Chunks Analyzed: {metrics.TotalChunks}");
        
        if (metrics.AgentMetrics.Any())
        {
            Console.WriteLine("\nPer-Agent Metrics:");
            foreach (var agent in metrics.AgentMetrics)
            {
                Console.WriteLine($"  {agent.AgentName}:");
                Console.WriteLine($"    Calls: {agent.TotalCalls}");
                Console.WriteLine($"    Network Time: {agent.TotalNetworkTimeMs}ms (avg: {agent.AverageNetworkTimeMs:F0}ms)");
                Console.WriteLine($"    Calculation Time: {agent.TotalCalculationTimeMs}ms (avg: {agent.AverageCalculationTimeMs:F0}ms)");
                Console.WriteLine($"    Tokens: {agent.TotalTokens} (avg: {agent.AverageTokensPerCall:F0}/call)");
            }
        }
    }
}

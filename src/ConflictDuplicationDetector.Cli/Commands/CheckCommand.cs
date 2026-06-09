using System.CommandLine;
using ConflictDuplicationDetector.Core.Documents;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;

namespace ConflictDuplicationDetector.Cli.Commands;

public class CheckCommand : Command
{
    public CheckCommand(Option<string?> providerOption) : base("check", "Analyse a file against the existing knowledge base for conflicts, duplications, and inconsistencies")
    {
        var fileArgument = new Argument<string>("file", "Path to the file to analyse against the knowledge base");
        var typeOption = new Option<string?>("--type", "Analysis type: all, duplications, conflicts, inconsistencies");
        var outputOption = new Option<string?>("--output", "Output file path for results (JSON)");
        var configOption = new Option<string?>("--config", "Path to configuration file");

        AddArgument(fileArgument);
        AddOption(typeOption);
        AddOption(outputOption);
        AddOption(configOption);

        this.SetHandler(ExecuteAsync, fileArgument, typeOption, outputOption, configOption, providerOption);
    }

    private async Task ExecuteAsync(string filePath, string? type, string? outputPath, string? configPath, string? provider)
    {
        Console.WriteLine("File Analysis Against Knowledge Base");
        Console.WriteLine("=====================================");

        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: File not found: {filePath}");
            Console.ResetColor();
            return;
        }

        var config = ConfigurationLoader.Load(configPath, provider);

        if (string.IsNullOrEmpty(config.OpenAI.ApiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: API key not configured. Set OPENAI_API_KEY environment variable or configure in appsettings.json");
            Console.ResetColor();
            return;
        }

        PrintProviderInfo(config);

        var vectorStore = new SharpVectorStore(config.OpenAI);

        if (!File.Exists(config.VectorStore.PersistPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No documents have been ingested yet. Run 'ingest' command first to build the knowledge base.");
            Console.ResetColor();
            return;
        }

        await vectorStore.LoadAsync(config.VectorStore.PersistPath);
        var kbChunkCount = await vectorStore.GetChunkCountAsync();

        if (kbChunkCount == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Knowledge base is empty. Run 'ingest' command first.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Knowledge base: {kbChunkCount} chunks loaded.");

        var parserFactory = new DocumentParserFactory();
        var parser = parserFactory.GetParser(filePath);

        if (parser == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Unsupported file type: {Path.GetExtension(filePath)}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Analyzing: {Path.GetFileName(filePath)}");
        Console.WriteLine();

        var document = await parser.ParseAsync(filePath);
        var chunker = new DocumentChunker();
        var fileChunks = chunker.ChunkDocument(document, config.Analysis.ChunkSize, config.Analysis.ChunkOverlap).ToList();

        Console.WriteLine($"File parsed into {fileChunks.Count} chunks.");

        var clientFactory = new AIClientFactory();
        var chatClient = clientFactory.CreateChatClient(config.OpenAI);
        var metricsTracker = new MetricsTracker();

        var analysisService = new FileAnalysisService(
            chatClient,
            vectorStore,
            metricsTracker,
            config.Analysis);

        var analysisType = type?.ToLowerInvariant() ?? "all";
        Console.WriteLine($"Running {analysisType} analysis against knowledge base...");
        Console.WriteLine();

        var result = await analysisService.AnalyzeFileAsync(fileChunks, document, analysisType);

        PrintResults(result, Path.GetFileName(filePath));
        PrintMetrics(result.Metrics, config.OpenAI.Model, config.OpenAI.EmbeddingModel);

        if (!string.IsNullOrEmpty(outputPath))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine($"\nResults saved to: {outputPath}");
        }
    }

    private static void PrintResults(AnalysisResult result, string fileName)
    {
        Console.WriteLine($"Analysis Results for: {fileName}");
        Console.WriteLine(new string('-', 40));

        if (result.Duplications.Any())
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nDuplications Found: {result.Duplications.Count}");
            Console.ResetColor();

            foreach (var dup in result.Duplications.Take(15))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  [{dup.Type}] Similarity: {dup.SimilarityScore:P0}");
                Console.ResetColor();

                Console.WriteLine($"    In file:");
                PrintLocation(dup.Source);
                if (!string.IsNullOrEmpty(dup.SourceExcerpt))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      \"{TruncateForDisplay(dup.SourceExcerpt, 100)}\"");
                    Console.ResetColor();
                }

                Console.WriteLine($"    In knowledge base: {dup.Target.FileName}");
                PrintLocation(dup.Target);
                if (!string.IsNullOrEmpty(dup.TargetExcerpt))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      \"{TruncateForDisplay(dup.TargetExcerpt, 100)}\"");
                    Console.ResetColor();
                }
            }

            if (result.Duplications.Count > 15)
                Console.WriteLine($"\n  ... and {result.Duplications.Count - 15} more");
        }
        else
        {
            Console.WriteLine("\nNo duplications found against knowledge base.");
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

                Console.WriteLine($"    In file:");
                PrintLocation(conflict.Source);
                if (!string.IsNullOrEmpty(conflict.SourceStatement))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      \"{TruncateForDisplay(conflict.SourceStatement, 100)}\"");
                    Console.ResetColor();
                }

                Console.WriteLine($"    In knowledge base: {conflict.Target.FileName}");
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
            Console.WriteLine("\nNo conflicts found against knowledge base.");
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
            Console.WriteLine("\nNo inconsistencies found against knowledge base.");
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

    private static void PrintMetrics(AnalysisMetrics metrics, string chatModel, string embeddingModel)
    {
        Console.WriteLine();
        Console.WriteLine("Performance Metrics");
        Console.WriteLine("-------------------");

        var totalTokens = metrics.AgentMetrics.Sum(a => a.TotalTokens);

        Console.WriteLine($"Total Duration: {metrics.TotalDuration.TotalSeconds:F2}s | Total Tokens: {totalTokens:N0}");
        Console.WriteLine($"Chunks Analyzed: {metrics.TotalChunks}");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"Models Used:");
        Console.WriteLine($"  Chat/Analysis: {chatModel}");
        Console.WriteLine($"  Embeddings: {embeddingModel}");
        Console.ResetColor();

        if (metrics.AgentMetrics.Any())
        {
            Console.WriteLine("\nPer-Agent Metrics:");
            foreach (var agent in metrics.AgentMetrics)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  {agent.AgentName}:");
                Console.ResetColor();
                Console.WriteLine($"    Model: {chatModel}");
                Console.WriteLine($"    Calls: {agent.TotalCalls}");
                Console.WriteLine($"    Network Time: {agent.TotalNetworkTimeMs}ms (avg: {agent.AverageNetworkTimeMs:F0}ms)");
                Console.WriteLine($"    Calculation Time: {agent.TotalCalculationTimeMs}ms (avg: {agent.AverageCalculationTimeMs:F0}ms)");
                Console.WriteLine($"    Tokens: {agent.TotalTokens:N0} (input: {agent.TotalInputTokens:N0}, output: {agent.TotalOutputTokens:N0})");
            }
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

    private static void PrintProviderInfo(AppConfiguration config)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"Provider: {config.OpenAI.Provider}");
        if (config.OpenAI.UseAzure && !string.IsNullOrEmpty(config.OpenAI.AzureEndpoint))
        {
            Console.WriteLine($"Endpoint: {config.OpenAI.AzureEndpoint}");
        }
        Console.ResetColor();
        Console.WriteLine();
    }
}

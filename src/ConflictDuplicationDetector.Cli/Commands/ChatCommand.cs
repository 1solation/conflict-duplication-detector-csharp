using System.CommandLine;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ConflictDuplicationDetector.Cli.Commands;

public class ChatCommand : Command
{
    public ChatCommand() : base("chat", "Start interactive chat session for document analysis")
    {
        var configOption = new Option<string?>("--config", "Path to configuration file");
        
        AddOption(configOption);
        
        this.SetHandler(ExecuteAsync, configOption);
    }
    
    private async Task ExecuteAsync(string? configPath)
    {
        Console.WriteLine("Interactive Document Analysis Chat");
        Console.WriteLine("===================================");
        Console.WriteLine();
        
        var config = ConfigurationLoader.Load(configPath);
        
        if (string.IsNullOrEmpty(config.OpenAI.ApiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: OpenAI API key not configured. Set OPENAI_API_KEY environment variable or configure in appsettings.json");
            Console.ResetColor();
            return;
        }
        
        var vectorStore = new SharpVectorStore(config.OpenAI.ApiKey, config.OpenAI.EmbeddingModel);
        
        if (File.Exists(config.VectorStore.PersistPath))
        {
            await vectorStore.LoadAsync(config.VectorStore.PersistPath);
            var chunkCount = await vectorStore.GetChunkCountAsync();
            Console.WriteLine($"Loaded {chunkCount} document chunks from knowledge base.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warning: No documents have been ingested yet. Run 'ingest' command first for best results.");
            Console.ResetColor();
        }
        
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  Type your question to analyze documents");
        Console.WriteLine("  'help' - Show available commands");
        Console.WriteLine("  'stats' - Show knowledge base statistics");
        Console.WriteLine("  'clear' - Clear the screen");
        Console.WriteLine("  'exit' or 'quit' - Exit the chat");
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
        
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You: ");
            Console.ResetColor();
            
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
                continue;
                
            switch (input.ToLowerInvariant())
            {
                case "exit":
                case "quit":
                case "q":
                    Console.WriteLine("Goodbye!");
                    return;
                    
                case "help":
                    PrintHelp();
                    continue;
                    
                case "stats":
                    await PrintStatsAsync(vectorStore, metricsTracker);
                    continue;
                    
                case "clear":
                    Console.Clear();
                    continue;
            }
            
            try
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Analyzing...");
                Console.ResetColor();
                
                var response = await analysisService.ChatAsync(input);
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Assistant: ");
                Console.ResetColor();
                Console.WriteLine(response.Message);
                
                if (response.ResultCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[Found {response.ResultCount} result(s) - Intent: {response.Intent}]");
                    Console.ResetColor();
                }
                
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }
    }
    
    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  'Find duplicates' - Search for duplicate content");
        Console.WriteLine("  'Find conflicts' - Search for contradictions");
        Console.WriteLine("  'Find inconsistencies' - Search for terminology/format issues");
        Console.WriteLine("  'Run full analysis' - Run complete analysis");
        Console.WriteLine("  Or ask any question about your documents");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  'Are there any duplicate paragraphs?'");
        Console.WriteLine("  'Find conflicts related to pricing'");
        Console.WriteLine("  'What terminology inconsistencies exist?'");
        Console.WriteLine("  'Summarize the main topics in the documents'");
        Console.WriteLine();
    }
    
    private static async Task PrintStatsAsync(IVectorStore vectorStore, MetricsTracker metricsTracker)
    {
        Console.WriteLine();
        Console.WriteLine("Knowledge Base Statistics");
        Console.WriteLine("-------------------------");
        Console.WriteLine($"Total chunks: {await vectorStore.GetChunkCountAsync()}");
        
        var metrics = metricsTracker.GetAllMetrics();
        if (metrics.Any())
        {
            Console.WriteLine("\nSession Metrics:");
            var totalCalls = metrics.Sum(m => m.TotalCalls);
            var totalTokens = metrics.Sum(m => m.TotalTokens);
            var totalNetworkTime = metrics.Sum(m => m.TotalNetworkTimeMs);
            
            Console.WriteLine($"  Total API calls: {totalCalls}");
            Console.WriteLine($"  Total tokens used: {totalTokens}");
            Console.WriteLine($"  Total network time: {totalNetworkTime}ms");
        }
        
        Console.WriteLine();
    }
}

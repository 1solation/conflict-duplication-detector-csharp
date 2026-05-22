using System.CommandLine;
using ConflictDuplicationDetector.Core.Documents;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using ConflictDuplicationDetector.Core.VectorStore;

namespace ConflictDuplicationDetector.Cli.Commands;

public class IngestCommand : Command
{
    public IngestCommand() : base("ingest", "Ingest documents into the vector store")
    {
        var pathArgument = new Argument<string>("path", "Path to file or directory to ingest");
        var recursiveOption = new Option<bool>("--recursive", () => true, "Recursively process subdirectories");
        var configOption = new Option<string?>("--config", "Path to configuration file");
        
        AddArgument(pathArgument);
        AddOption(recursiveOption);
        AddOption(configOption);
        
        this.SetHandler(ExecuteAsync, pathArgument, recursiveOption, configOption);
    }
    
    private async Task ExecuteAsync(string path, bool recursive, string? configPath)
    {
        Console.WriteLine("Document Ingestion");
        Console.WriteLine("==================");
        
        var config = ConfigurationLoader.Load(configPath);
        
        if (string.IsNullOrEmpty(config.OpenAI.ApiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: OpenAI API key not configured. Set OPENAI_API_KEY environment variable or configure in appsettings.json");
            Console.ResetColor();
            return;
        }
        
        var parserFactory = new DocumentParserFactory();
        var chunker = new DocumentChunker();
        var vectorStore = new SharpVectorStore(config.OpenAI.ApiKey, config.OpenAI.EmbeddingModel);
        
        if (File.Exists(config.VectorStore.PersistPath))
        {
            Console.WriteLine($"Loading existing vector store from {config.VectorStore.PersistPath}...");
            await vectorStore.LoadAsync(config.VectorStore.PersistPath);
        }
        
        var ingestionService = new DocumentIngestionService(parserFactory, chunker, vectorStore, config.Analysis);
        
        IngestionResult result;
        
        if (File.Exists(path))
        {
            Console.WriteLine($"Ingesting file: {path}");
            result = await ingestionService.IngestFileAsync(path);
        }
        else if (Directory.Exists(path))
        {
            Console.WriteLine($"Ingesting directory: {path} (recursive: {recursive})");
            result = await ingestionService.IngestDirectoryAsync(path, recursive);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Path not found: {path}");
            Console.ResetColor();
            return;
        }
        
        await vectorStore.SaveAsync(config.VectorStore.PersistPath);
        
        Console.WriteLine();
        Console.WriteLine("Ingestion Results");
        Console.WriteLine("-----------------");
        Console.WriteLine($"Documents processed: {result.DocumentsProcessed}");
        Console.WriteLine($"Chunks created: {result.ChunksCreated}");
        Console.WriteLine($"Chunks skipped (duplicates): {result.ChunksSkipped}");
        Console.WriteLine($"Total chunks in store: {await vectorStore.GetChunkCountAsync()}");
        
        if (result.Errors.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nWarnings/Errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
            Console.ResetColor();
        }
        
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nIngestion completed successfully!");
            Console.ResetColor();
        }
    }
}

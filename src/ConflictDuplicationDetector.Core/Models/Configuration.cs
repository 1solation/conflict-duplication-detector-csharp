namespace ConflictDuplicationDetector.Core.Models;

public class AppConfiguration
{
    public OpenAIConfiguration OpenAI { get; set; } = new();
    public VectorStoreConfiguration VectorStore { get; set; } = new();
    public AnalysisConfiguration Analysis { get; set; } = new();
}

public class OpenAIConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string? AzureEndpoint { get; set; }
    public string? AzureApiVersion { get; set; }
    public string Model { get; set; } = "gpt-4o";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public bool UseAzure => !string.IsNullOrEmpty(AzureEndpoint);
}

public class VectorStoreConfiguration
{
    public string PersistPath { get; set; } = "./data/vectors.json";
    public int MaxSearchResults { get; set; } = 10;
}

public class AnalysisConfiguration
{
    public double DuplicationThreshold { get; set; } = 0.85;
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;
    public int MaxConcurrentAgents { get; set; } = 3;
}

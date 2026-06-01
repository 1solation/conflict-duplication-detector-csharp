using ConflictDuplicationDetector.Core.Models;
using Xunit;

namespace ConflictDuplicationDetector.Tests.Models;

public class ConfigurationTests
{
    [Fact]
    public void AppConfiguration_HasDefaultValues()
    {
        var config = new AppConfiguration();

        Assert.NotNull(config.OpenAI);
        Assert.NotNull(config.VectorStore);
        Assert.NotNull(config.Analysis);
    }

    [Fact]
    public void OpenAIConfiguration_HasDefaultModel()
    {
        var config = new OpenAIConfiguration();

        Assert.Equal("gpt-5-nano", config.Model);
        Assert.Equal("text-embedding-3-small", config.EmbeddingModel);
    }

    [Fact]
    public void OpenAIConfiguration_UseAzure_WhenEndpointSet()
    {
        var config = new OpenAIConfiguration
        {
            AzureEndpoint = "https://myendpoint.openai.azure.com"
        };

        Assert.True(config.UseAzure);
    }

    [Fact]
    public void OpenAIConfiguration_NotUseAzure_WhenEndpointNull()
    {
        var config = new OpenAIConfiguration
        {
            AzureEndpoint = null
        };

        Assert.False(config.UseAzure);
    }

    [Fact]
    public void VectorStoreConfiguration_HasDefaultPath()
    {
        var config = new VectorStoreConfiguration();

        Assert.Equal("./data/vectors.json", config.PersistPath);
        Assert.Equal(10, config.MaxSearchResults);
    }

    [Fact]
    public void AnalysisConfiguration_HasDefaultThresholds()
    {
        var config = new AnalysisConfiguration();

        Assert.Equal(0.85, config.DuplicationThreshold);
        Assert.Equal(512, config.ChunkSize);
        Assert.Equal(50, config.ChunkOverlap);
        Assert.Equal(3, config.MaxConcurrentAgents);
    }
}

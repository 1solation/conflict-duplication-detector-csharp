using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using ConflictDuplicationDetector.Core.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace ConflictDuplicationDetector.Core.Services;

public interface IAIClientFactory
{
    IChatClient CreateChatClient(OpenAIConfiguration config);
    EmbeddingClient CreateEmbeddingClient(OpenAIConfiguration config);
}

public class AIClientFactory : IAIClientFactory
{
    public IChatClient CreateChatClient(OpenAIConfiguration config)
    {
        ValidateConfig(config);

        if (config.UseAzure)
        {
            return CreateAzureChatClient(config);
        }

        var client = new OpenAIClient(config.ApiKey);
        return client.GetChatClient(config.Model).AsIChatClient();
    }

    public EmbeddingClient CreateEmbeddingClient(OpenAIConfiguration config)
    {
        ValidateConfig(config);

        if (config.UseAzure)
        {
            return CreateAzureEmbeddingClient(config);
        }

        var client = new OpenAIClient(config.ApiKey);
        return client.GetEmbeddingClient(config.EmbeddingModel);
    }

    private static IChatClient CreateAzureChatClient(OpenAIConfiguration config)
    {
        var endpoint = new Uri(config.AzureEndpoint!);
        var credential = new ApiKeyCredential(config.ApiKey);

        var options = new AzureOpenAIClientOptions();
        
        if (!string.IsNullOrEmpty(config.ApiKeyHeader))
        {
            options.AddPolicy(new CustomApiKeyHeaderPolicy(config.ApiKeyHeader, config.ApiKey), PipelinePosition.PerCall);
        }

        var client = new AzureOpenAIClient(endpoint, credential, options);
        return client.GetChatClient(config.Model).AsIChatClient();
    }

    private static EmbeddingClient CreateAzureEmbeddingClient(OpenAIConfiguration config)
    {
        var endpoint = new Uri(config.AzureEndpoint!);
        var credential = new ApiKeyCredential(config.ApiKey);

        var options = new AzureOpenAIClientOptions();
        
        if (!string.IsNullOrEmpty(config.ApiKeyHeader))
        {
            options.AddPolicy(new CustomApiKeyHeaderPolicy(config.ApiKeyHeader, config.ApiKey), PipelinePosition.PerCall);
        }

        var client = new AzureOpenAIClient(endpoint, credential, options);
        return client.GetEmbeddingClient(config.EmbeddingModel);
    }

    private static void ValidateConfig(OpenAIConfiguration config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException(
                "API key not configured. Set OPENAI_API_KEY environment variable or configure OpenAI:ApiKey in appsettings.json");
        }

        if (config.UseAzure && string.IsNullOrEmpty(config.AzureEndpoint))
        {
            throw new InvalidOperationException(
                "Azure endpoint not configured. Set OpenAI:AzureEndpoint in appsettings.json or environment variable OpenAI__AzureEndpoint");
        }
    }
}

internal class CustomApiKeyHeaderPolicy : PipelinePolicy
{
    private readonly string _headerName;
    private readonly string _apiKey;

    public CustomApiKeyHeaderPolicy(string headerName, string apiKey)
    {
        _headerName = headerName;
        _apiKey = apiKey;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set(_headerName, _apiKey);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set(_headerName, _apiKey);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }
}

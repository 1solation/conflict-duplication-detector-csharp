using ConflictDuplicationDetector.Core.Configuration;
using ConflictDuplicationDetector.Core.Documents;
using ConflictDuplicationDetector.Core.Models;
using ConflictDuplicationDetector.Core.Services;
using ConflictDuplicationDetector.Core.VectorStore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace ConflictDuplicationDetector.Core.DependencyInjection;

public static class DetectorServiceCollectionExtensions
{
    public static IServiceCollection AddConflictDuplicationDetector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var appConfig = ConfigurationLoader.Bind(configuration);
        services.AddSingleton(appConfig);
        services.AddSingleton(appConfig.OpenAI);
        services.AddSingleton(appConfig.VectorStore);
        services.AddSingleton(appConfig.Analysis);
        services.AddSingleton(appConfig.Storage);

        services.AddSingleton<IVectorStore>(sp =>
        {
            var config = sp.GetRequiredService<AppConfiguration>();
            return new SharpVectorStore(config.OpenAI.ApiKey, config.OpenAI.EmbeddingModel);
        });

        services.AddSingleton<IVectorStoreCoordinator, VectorStoreCoordinator>();
        services.AddSingleton<IDocumentParserFactory, DocumentParserFactory>();
        services.AddSingleton<IDocumentChunker, DocumentChunker>();
        services.AddSingleton<MetricsTracker>();

        services.AddSingleton<IChatClient>(sp =>
        {
            var config = sp.GetRequiredService<AppConfiguration>();
            var openAiClient = new OpenAIClient(config.OpenAI.ApiKey);
            return openAiClient.GetChatClient(config.OpenAI.Model).AsIChatClient();
        });

        services.AddSingleton<IDocumentIngestionService, DocumentIngestionService>();
        services.AddSingleton<IFileAnalysisService, FileAnalysisService>();

        services.AddSingleton<IAnalysisService>(sp =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            var coordinator = sp.GetRequiredService<IVectorStoreCoordinator>();
            var metricsTracker = sp.GetRequiredService<MetricsTracker>();
            var analysisConfig = sp.GetRequiredService<AnalysisConfiguration>();
            var appConfiguration = sp.GetRequiredService<AppConfiguration>();
            return new AnalysisService(
                chatClient,
                coordinator.VectorStore,
                metricsTracker,
                analysisConfig,
                appConfiguration.VectorStore.PersistPath);
        });

        return services;
    }
}

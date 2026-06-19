using ConflictDuplicationDetector.Api.Models;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Api.Pages;

public class KnowledgeBaseModel(AppConfiguration config, IServiceProvider services)
    : DetectorPageModel(config, services)
{
    public KnowledgeBaseDetailsResponse? KnowledgeBase { get; private set; }

    public bool OpenAiConfigured => IsOpenAiConfigured;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetDetector(out var detector))
        {
            KnowledgeBase = new KnowledgeBaseDetailsResponse(
                ProviderFormatter.Format(AppConfig.OpenAI),
                System.IO.File.Exists(AppConfig.VectorStore.PersistPath),
                0,
                0,
                AppConfig.VectorStore.PersistPath,
                [],
                []);
            return;
        }

        KnowledgeBase = await detector.GetKnowledgeBaseDetailsAsync(cancellationToken);
    }
}
